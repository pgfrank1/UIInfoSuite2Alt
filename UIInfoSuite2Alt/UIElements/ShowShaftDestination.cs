using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using UIInfoSuite2Alt.Infrastructure;
using UIInfoSuite2Alt.Infrastructure.Helpers;

namespace UIInfoSuite2Alt.UIElements;

internal class ShowShaftDestination : IDisposable
{
  private const int ShaftTileIndex = 174;
  private const string MineBuildingsLayer = "Buildings";
  private const string MineTilesheetId = "mine";
  private const int QuarryMineShaft = 77377;
  private const int SkullCavernDisplayOffset = 120;
  private const int DamagePerFloor = 3;

  private readonly IModHelper _helper;
  private readonly PerScreen<Vector2?> _hoveredTile = new();
  private readonly PerScreen<int> _predictedFall = new();
  private readonly PerScreen<int> _destinationFloor = new();

  // Prediction is identical for every shaft on a given floor/day; cache per level.
  private readonly PerScreen<int?> _cachedMineLevel = new();

  public ShowShaftDestination(IModHelper helper)
  {
    _helper = helper;
  }

  public void Dispose()
  {
    ToggleOption(false);
  }

  public void ToggleOption(bool enabled)
  {
    _helper.Events.Display.RenderingHud -= OnRenderingHud;
    _helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;

    if (!enabled)
    {
      return;
    }

    _helper.Events.Display.RenderingHud += OnRenderingHud;
    _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
  }

  private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
  {
    if (!e.IsMultipleOf(4))
    {
      return;
    }

    _hoveredTile.Value = null;

    if (Game1.currentLocation is not MineShaft mine)
    {
      _cachedMineLevel.Value = null;
      return;
    }

    // Skull Cavern only; skip the quarry mine shaft location.
    if (mine.mineLevel < 121 || mine.mineLevel == QuarryMineShaft)
    {
      _cachedMineLevel.Value = null;
      return;
    }

    Vector2 mouseTile = Game1.currentCursorTile;
    Vector2 gamepadTile =
      Game1.player.CurrentTool != null
        ? Utility.snapToInt(Game1.player.GetToolLocation() / Game1.tileSize)
        : Utility.snapToInt(Game1.player.GetGrabTile());
    Vector2 tile =
      Game1.options.gamepadControls && Game1.timerUntilMouseFade <= 0 ? gamepadTile : mouseTile;

    int tileIndex = mine.getTileIndexAt(
      (int)tile.X,
      (int)tile.Y,
      MineBuildingsLayer,
      MineTilesheetId
    );
    if (tileIndex != ShaftTileIndex)
    {
      return;
    }

    _hoveredTile.Value = tile;

    if (_cachedMineLevel.Value == mine.mineLevel)
    {
      return;
    }

    int fall = ShaftPredictor.PredictFallDistance(mine.mineLevel);
    _predictedFall.Value = fall;
    _destinationFloor.Value = mine.mineLevel + fall - SkullCavernDisplayOffset;
    _cachedMineLevel.Value = mine.mineLevel;
  }

  private void OnRenderingHud(object? sender, RenderingHudEventArgs e)
  {
    if (!UIElementUtils.IsRenderingNormally() || Game1.activeClickableMenu != null)
    {
      return;
    }

    Vector2? tile = _hoveredTile.Value;
    if (tile == null)
    {
      return;
    }

    int damage = Math.Min(
      _predictedFall.Value * DamagePerFloor,
      Math.Max(0, Game1.player.health - 1)
    );
    DrawTooltip(
      Game1.spriteBatch,
      tile.Value,
      _predictedFall.Value,
      _destinationFloor.Value,
      damage
    );
  }

  private static void DrawTooltip(
    SpriteBatch b,
    Vector2 hoverTile,
    int fallFloors,
    int destination,
    int damage
  )
  {
    SpriteFont font = Game1.smallFont;

    string floorPart = I18n.ShaftDestination_Floor(floor: destination) + " ";
    string fallPart = $"+{fallFloors} ";
    string damagePart = I18n.ShaftDestination_Damage(hp: damage);
    Vector2 floorSize = font.MeasureString(floorPart);
    Vector2 fallSize = font.MeasureString(fallPart);
    Vector2 damageSize = font.MeasureString(damagePart);

    int width = (int)(floorSize.X + fallSize.X + damageSize.X) + 32 + 4;
    int height = font.LineSpacing + 38;

    int overrideX = -1;
    int overrideY = -1;
    if (Game1.options.gamepadControls && Game1.timerUntilMouseFade <= 0)
    {
      Vector2 tilePx = Utility.ModifyCoordinatesForUIScale(
        Game1.GlobalToLocal(hoverTile * Game1.tileSize)
      );
      overrideX = (int)(tilePx.X + Utility.ModifyCoordinateForUIScale(32));
      overrideY = (int)(tilePx.Y + Utility.ModifyCoordinateForUIScale(32));
    }

    int x = overrideX != -1 ? overrideX : Game1.getOldMouseX() + 32;
    int y = overrideY != -1 ? overrideY : Game1.getOldMouseY() + 32;

    Rectangle safeArea = Utility.getSafeArea();
    if (x + width > safeArea.Right)
    {
      x = safeArea.Right - width;
      y += 16;
    }

    if (y + height > safeArea.Bottom)
    {
      x += 16;
      if (x + width > safeArea.Right)
      {
        x = safeArea.Right - width;
      }

      y = safeArea.Bottom - height;
    }

    IClickableMenu.drawTextureBox(
      b,
      Game1.menuTexture,
      new Rectangle(0, 256, 60, 60),
      x,
      y,
      width,
      height,
      Color.White
    );

    float textX = x + 16;
    float textY = y + 20;
    Tools.DrawShadowedText(
      b,
      font,
      floorPart,
      new Vector2(textX, textY),
      Game1.textColor,
      Game1.textShadowColor
    );
    Tools.DrawShadowedText(
      b,
      font,
      fallPart,
      new Vector2(textX + floorSize.X, textY),
      Tools.TooltipYellow,
      Game1.textShadowColor
    );
    Tools.DrawShadowedText(
      b,
      font,
      damagePart,
      new Vector2(textX + floorSize.X + fallSize.X, textY),
      Tools.TooltipRed,
      Game1.textShadowColor
    );
  }
}
