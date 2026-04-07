using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.TerrainFeatures;
using UIInfoSuite2Alt.Infrastructure;
using UIInfoSuite2Alt.Compatibility;
using Object = StardewValley.Object;

namespace UIInfoSuite2Alt.UIElements;

internal class ShowItemEffectRanges : IDisposable
{
  #region Properties
  private readonly PerScreen<List<Point>> _effectiveAreaCurrent = new(() => []);
  private readonly PerScreen<HashSet<Point>> _effectiveAreaOther = new(() => []);
  private readonly PerScreen<HashSet<Point>> _effectiveAreaIntersection = new(() => []);
  private readonly PerScreen<HashSet<Point>> _seenTiles = new(() => []);

  private readonly IModHelper _helper;
  private readonly Lazy<Texture2D> _tileTexture;
  private readonly Lazy<Texture2D> _tileBombTexture;
  private readonly Lazy<Texture2D> _wildTreeTexture;
  private readonly PerScreen<bool> _isBombRange = new(() => false);

  private int _junimoHutRadius = 8; // default radius
  private bool _showItemEffectRanges;
  private bool _showPlacedItemRanges = true;

  private bool ButtonControlShow { get; set; }
  private bool ShowRangeTooltip { get; set; } = true;
  private bool ShowBombRange { get; set; }

  private bool ButtonShowOneRange { get; set; }
  private bool ButtonShowAllRanges { get; set; }

  private readonly PerScreen<RangeTooltipInfo?> _rangeTooltipInfo = new(() => null);

  /// <summary>Whether the range tooltip is currently visible. Used by ShowTileTooltips to avoid overlapping tooltips.</summary>
  public bool IsRangeTooltipActive => ShowRangeTooltip && _rangeTooltipInfo.Value != null;

  private sealed class RangeTooltipInfo
  {
    public string ObjectName = "";
    public bool TrackOverlap;
    public int ObjectCount;
    public bool ShowingAll;
    public int OccupiedTiles;
    public int RawTotalTiles;
    public string? SubHeader;
    public string? WarningMessage;
    public Color TileColor = Color.LawnGreen;
    public Texture2D? SpriteTexture;
    public Rectangle? SpriteSourceRect;

    public void SetSpriteFromObject(Object obj)
    {
      var itemData = ItemRegistry.GetData(obj.QualifiedItemId);
      if (itemData != null)
      {
        SpriteTexture = itemData.GetTexture();
        SpriteSourceRect = itemData.GetSourceRect();
      }
    }
  }
  #endregion


  #region Lifecycle
  public ShowItemEffectRanges(IModHelper helper)
  {
    _helper = helper;
    _tileTexture = new Lazy<Texture2D>(() =>
      AssetHelper.TryLoadTexture(_helper, "assets/tile.png")
    );
    _tileBombTexture = new Lazy<Texture2D>(() =>
      AssetHelper.TryLoadTexture(_helper, "assets/tile_muted.png")
    );
    _wildTreeTexture = new Lazy<Texture2D>(() =>
      AssetHelper.TryLoadTexture(_helper, "assets/wild_tree_tooltip.png")
    );
  }

  public void Dispose()
  {
    ToggleOption(false);
    ToggleShowBombRangeOption(false);
  }

  public void ToggleOption(bool showItemEffectRanges)
  {
    _showItemEffectRanges = showItemEffectRanges;
    UpdateEventSubscriptions();
  }

  public void ToggleShowPlacedItemRangesOption(bool showPlacedItemRanges)
  {
    _showPlacedItemRanges = showPlacedItemRanges;
  }

  public void ToggleButtonControlShowOption(bool buttonControlShow)
  {
    ButtonControlShow = buttonControlShow;

    _helper.Events.Input.ButtonsChanged -= OnButtonChanged;
    if (buttonControlShow)
    {
      _helper.Events.Input.ButtonsChanged += OnButtonChanged;
    }

    UpdateEventSubscriptions();
  }

  public void ToggleShowRangeTooltipOption(bool showRangeTooltip)
  {
    ShowRangeTooltip = showRangeTooltip;
  }

  public void ToggleShowBombRangeOption(bool showBombRange)
  {
    ShowBombRange = showBombRange;
    UpdateEventSubscriptions();
  }

  private void UpdateEventSubscriptions()
  {
    _helper.Events.Display.RenderingHud -= OnRenderingHud;
    _helper.Events.Display.RenderedHud -= OnRenderedHud;
    _helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;

    if (_showItemEffectRanges || ShowBombRange || ButtonControlShow)
    {
      _helper.Events.Display.RenderingHud += OnRenderingHud;
      _helper.Events.Display.RenderedHud += OnRenderedHud;
      _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }
  }
  #endregion


  #region Event subscriptions
  private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
  {
    if (!e.IsMultipleOf(4))
    {
      return;
    }

    // Guard against ticks during loading screen
    if (Game1.currentLocation is null)
    {
      return;
    }

    _effectiveAreaCurrent.Value.Clear();
    _effectiveAreaOther.Value.Clear();
    _effectiveAreaIntersection.Value.Clear();
    _seenTiles.Value.Clear();
    _isBombRange.Value = false;

    if (Game1.activeClickableMenu == null && UIElementUtils.IsRenderingNormally())
    {
      UpdateEffectiveArea();
      GetOverlapValue();

      // When overlap tracking is off (e.g. wild trees), merge intersection back into
      // the normal area so all tiles render green instead of red.
      if (
        _rangeTooltipInfo.Value is { TrackOverlap: false }
        && _effectiveAreaIntersection.Value.Count > 0
      )
      {
        _effectiveAreaOther.Value.UnionWith(_effectiveAreaIntersection.Value);
        _effectiveAreaIntersection.Value.Clear();
      }
      if (ButtonShowOneRange)
      {
        ButtonShowOneRange = false;
      }

      if (ButtonShowAllRanges)
      {
        ButtonShowAllRanges = false;
      }
    }
  }

  private void OnRenderingHud(object? sender, RenderingHudEventArgs e)
  {
    Color tileColor = _isBombRange.Value
      ? Color.Lime
      : _rangeTooltipInfo.Value?.TileColor ?? Color.LawnGreen;
    float tileOpacity = _isBombRange.Value ? 0.3f : 0.5f;
    Texture2D texture = _isBombRange.Value ? _tileBombTexture.Value : _tileTexture.Value;

    // Compute visible tile bounds to skip off-screen draw calls
    xTile.Dimensions.Rectangle viewport = Game1.viewport;
    int minTileX = viewport.X / Game1.tileSize - 1;
    int minTileY = viewport.Y / Game1.tileSize - 1;
    int maxTileX = (viewport.X + viewport.Width) / Game1.tileSize + 1;
    int maxTileY = (viewport.Y + viewport.Height) / Game1.tileSize + 1;

    // Placed items: faded, softer texture
    float otherOpacity = 0.5f;
    DrawTileHighlights(
      e,
      _effectiveAreaOther.Value,
      _tileBombTexture.Value,
      tileColor * otherOpacity,
      minTileX,
      minTileY,
      maxTileX,
      maxTileY
    );

    // Held item: prominent
    DrawTileHighlights(
      e,
      _effectiveAreaCurrent.Value,
      texture,
      tileColor * tileOpacity,
      minTileX,
      minTileY,
      maxTileX,
      maxTileY
    );

    // Overlap: orange
    DrawTileHighlights(
      e,
      _effectiveAreaIntersection.Value,
      texture,
      Color.DarkOrange * (tileOpacity + 0.2f),
      minTileX,
      minTileY,
      maxTileX,
      maxTileY
    );
  }

  private static void DrawTileHighlights(
    RenderingHudEventArgs e,
    IEnumerable<Point> tiles,
    Texture2D texture,
    Color color,
    int minTileX,
    int minTileY,
    int maxTileX,
    int maxTileY
  )
  {
    foreach (Point point in tiles)
    {
      if (point.X < minTileX || point.X > maxTileX || point.Y < minTileY || point.Y > maxTileY)
      {
        continue;
      }

      var position = new Vector2(
        point.X * Utility.ModifyCoordinateFromUIScale(Game1.tileSize),
        point.Y * Utility.ModifyCoordinateFromUIScale(Game1.tileSize)
      );
      e.SpriteBatch.Draw(
        texture,
        Utility.ModifyCoordinatesForUIScale(
          Game1.GlobalToLocal(Utility.ModifyCoordinatesForUIScale(position))
        ),
        null,
        color,
        0.0f,
        Vector2.Zero,
        Utility.ModifyCoordinateForUIScale(Game1.pixelZoom),
        SpriteEffects.None,
        0.01f
      );
    }
  }

  private void OnButtonChanged(object? sender, ButtonsChangedEventArgs e)
  {
    if (Context.IsPlayerFree)
    {
      if (ModEntry.ModConfig.ShowOneRange.IsDown())
      {
        ButtonShowOneRange = true;
      }

      if (ModEntry.ModConfig.ShowAllRange.IsDown())
      {
        ButtonShowAllRanges = true;
      }
    }
  }

  private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
  {
    if (!ShowRangeTooltip)
    {
      return;
    }

    RangeTooltipInfo? info = _rangeTooltipInfo.Value;
    if (info == null)
    {
      return;
    }

    int reachableTiles = info.RawTotalTiles - info.OccupiedTiles;
    int overlapTiles = _effectiveAreaIntersection.Value.Count;
    int coveredTiles = _effectiveAreaOther.Value.Count + overlapTiles - info.OccupiedTiles;

    SpriteFont font = Game1.smallFont;

    // Build tooltip lines
    string header = info.ShowingAll ? $"{info.ObjectName} x{info.ObjectCount}" : info.ObjectName;

    List<(string text, Color color)> lines = [(header, Game1.textColor)];

    if (info.SubHeader != null)
    {
      lines.Add((info.SubHeader, Tools.TooltipGreen));
    }

    if (info.WarningMessage != null)
    {
      lines.Add((info.WarningMessage, Tools.TooltipYellow));
    }
    else
    {
      lines.Add((I18n.ReachableTiles(count: reachableTiles), Tools.TooltipBlue));

      if (info.ShowingAll && info.TrackOverlap)
      {
        lines.Add((I18n.CoveredTiles(count: coveredTiles), Tools.TooltipGreen));
      }

      if (info.TrackOverlap && overlapTiles > 0)
      {
        lines.Add((I18n.OverlappingTiles(count: overlapTiles), Tools.TooltipYellow));
      }
    }

    // Sprite dimensions (same layout as ShowTileTooltips.DrawColoredHoverText)
    const int spriteSize = 32;
    const int spritePadding = 4;
    bool hasSprite = info.SpriteTexture != null && info.SpriteSourceRect != null;
    float spriteScale = hasSprite
      ? spriteSize
        / (float)Math.Max(info.SpriteSourceRect!.Value.Width, info.SpriteSourceRect.Value.Height)
      : 0;
    int renderedSpriteWidth = hasSprite
      ? (int)(info.SpriteSourceRect!.Value.Width * spriteScale)
      : 0;
    int spriteSpace = hasSprite ? renderedSpriteWidth + spritePadding : 0;

    // Calculate dimensions
    float maxWidth = 0;
    bool isFirstLine = true;
    foreach ((string text, Color _) in lines)
    {
      float w = font.MeasureString(text).X;
      if (isFirstLine)
      {
        w += spriteSpace;
        isFirstLine = false;
      }

      if (w > maxWidth)
      {
        maxWidth = w;
      }
    }

    int boxWidth = (int)maxWidth + 32;
    int boxHeight = Math.Max(60, lines.Count * font.LineSpacing + 40);

    // Position near mouse, keep on screen
    int x = Game1.getMouseX() + 32;
    int y = Game1.getMouseY() + 32;

    if (x + boxWidth > Game1.uiViewport.Width)
    {
      x = Game1.getMouseX() - boxWidth - 8;
    }

    if (y + boxHeight > Game1.uiViewport.Height)
    {
      y = Game1.uiViewport.Height - boxHeight;
    }

    boxWidth += 4;

    // Draw tooltip box
    IClickableMenu.drawTextureBox(
      e.SpriteBatch,
      Game1.menuTexture,
      new Rectangle(0, 256, 60, 60),
      x,
      y,
      boxWidth,
      boxHeight,
      Color.White
    );

    // Draw text lines with soft shadow (same layout as ShowTileTooltips)
    Color shadowColor = Game1.textShadowColor;
    float lineY = y + 16 + 4;
    bool isFirst = true;
    foreach ((string text, Color color) in lines)
    {
      float segX = x + 16;
      if (isFirst && hasSprite)
      {
        segX += spriteSpace;
      }

      var pos = new Vector2(segX, lineY);
      Tools.DrawShadowedText(e.SpriteBatch, font, text, pos, color, shadowColor);

      // Draw sprite on the first line, vertically centered with the text
      if (isFirst && hasSprite)
      {
        Rectangle sourceRect = info.SpriteSourceRect!.Value;
        float spriteCenterY =
          lineY + font.LineSpacing / 2f - (sourceRect.Height * spriteScale) / 2f - 2f;
        Vector2 spritePos = new(x + 16, spriteCenterY);
        e.SpriteBatch.Draw(
          info.SpriteTexture!,
          spritePos,
          sourceRect,
          Color.White,
          0f,
          Vector2.Zero,
          spriteScale,
          SpriteEffects.None,
          0.9f
        );
        isFirst = false;
      }

      lineY += font.LineSpacing;
    }
  }
  #endregion


  #region Logic
  private void UpdateEffectiveArea()
  {
    int[][] arrayToUse;
    List<Object> similarObjects;
    _rangeTooltipInfo.Value = null;

    if (ButtonControlShow && (ButtonShowOneRange || ButtonShowAllRanges))
    {
      Building building = Game1.currentLocation.getBuildingAt(Game1.GetPlacementGrabTile());

      if (building is JunimoHut hoveredHut)
      {
        // get the max radius real time to account for config changes
        if (ApiManager.GetApi(ModCompat.BetterJunimos, out IBetterJunimosApi? betterJunimosApi))
        {
          _junimoHutRadius = betterJunimosApi.GetJunimoHutMaxRadius();
        }

        arrayToUse = GetDistanceArray(ObjectsWithDistance.JunimoHut);
        int hutTiles = CountTilesInArray(arrayToUse);

        _rangeTooltipInfo.Value = new RangeTooltipInfo
        {
          ObjectName = I18n.JunimoHuts(),
          TrackOverlap = true,
          ObjectCount = 1,
          ShowingAll = ButtonShowAllRanges,
          OccupiedTiles = 6, // 3x2 building
          RawTotalTiles = hutTiles,
        };

        AddTilesToHighlightedArea(
          arrayToUse,
          !ButtonShowAllRanges,
          hoveredHut.tileX.Value + 1,
          hoveredHut.tileY.Value + 1
        );

        if (ButtonShowAllRanges)
        {
          foreach (Building? nextBuilding in Game1.currentLocation.buildings)
          {
            if (nextBuilding is JunimoHut nextHut && nextHut != hoveredHut)
            {
              _rangeTooltipInfo.Value.ObjectCount++;
              _rangeTooltipInfo.Value.OccupiedTiles += 6;
              _rangeTooltipInfo.Value.RawTotalTiles += hutTiles;
              AddTilesToHighlightedArea(
                arrayToUse,
                false,
                nextHut.tileX.Value + 1,
                nextHut.tileY.Value + 1
              );
            }
          }
        }
      }
    }

    // Wild tree seed spread — seed spread only occurs on Farm locations
    if (ButtonControlShow && (ButtonShowOneRange || ButtonShowAllRanges))
    {
      Vector2 gamepadTile =
        Game1.player.CurrentTool != null
          ? Utility.snapToInt(Game1.player.GetToolLocation() / Game1.tileSize)
          : Utility.snapToInt(Game1.player.GetGrabTile());
      Vector2 mouseTile = Game1.currentCursorTile;
      Vector2 treeTile =
        Game1.options.gamepadControls && Game1.timerUntilMouseFade <= 0 ? gamepadTile : mouseTile;

      if (
        Game1.currentLocation.terrainFeatures.TryGetValue(treeTile, out TerrainFeature? feature)
        && feature is Tree tree
        && tree.growthStage.Value >= 5
        && !tree.stump.Value
      )
      {
        float seedSpreadChance = tree.GetData()?.SeedSpreadChance ?? 0.15f;

        if (seedSpreadChance <= 0f)
        {
          string treeName = ShowTileTooltips.GetTreeTypeName(tree.treeType.Value) + I18n.Tree();
          _rangeTooltipInfo.Value = new RangeTooltipInfo
          {
            ObjectName = treeName,
            WarningMessage = I18n.NoSeedSpread(),
            SpriteTexture = _wildTreeTexture.Value,
            SpriteSourceRect = new Rectangle(0, 0, 12, 16),
          };
        }
        else if (Game1.currentLocation is Farm)
        {
          arrayToUse = GetDistanceArray(ObjectsWithDistance.WildTreeSeedSpread);

          string treeName = ButtonShowAllRanges
            ? I18n.WildTree()
            : ShowTileTooltips.GetTreeTypeName(tree.treeType.Value) + I18n.Tree();

          int tilesBeforeAdd = _effectiveAreaOther.Value.Count;
          AddTilesToHighlightedArea(
            arrayToUse,
            false,
            (int)treeTile.X,
            (int)treeTile.Y,
            skipOccupied: true
          );
          int reachableTiles = _effectiveAreaOther.Value.Count - tilesBeforeAdd;

          _rangeTooltipInfo.Value = new RangeTooltipInfo
          {
            ObjectName = treeName,
            SubHeader = I18n.SeedRange(),
            TrackOverlap = false,
            ObjectCount = 1,
            ShowingAll = ButtonShowAllRanges,
            OccupiedTiles = 0,
            RawTotalTiles = reachableTiles,
            SpriteTexture = _wildTreeTexture.Value,
            SpriteSourceRect = new Rectangle(0, 0, 12, 16),
          };

          if (ButtonShowAllRanges)
          {
            foreach (
              KeyValuePair<Vector2, TerrainFeature> pair in Game1
                .currentLocation
                .terrainFeatures
                .Pairs
            )
            {
              if (
                pair.Value is Tree otherTree
                && otherTree != tree
                && otherTree.growthStage.Value >= 5
                && !otherTree.stump.Value
                && (otherTree.GetData()?.SeedSpreadChance ?? 0.15f) > 0f
              )
              {
                _rangeTooltipInfo.Value.ObjectCount++;
                tilesBeforeAdd = _effectiveAreaOther.Value.Count;
                AddTilesToHighlightedArea(
                  arrayToUse,
                  false,
                  (int)pair.Key.X,
                  (int)pair.Key.Y,
                  skipOccupied: true
                );
                _rangeTooltipInfo.Value.RawTotalTiles +=
                  _effectiveAreaOther.Value.Count - tilesBeforeAdd;
              }
            }
          }
        }
        else
        {
          string treeName = ShowTileTooltips.GetTreeTypeName(tree.treeType.Value) + I18n.Tree();
          _rangeTooltipInfo.Value = new RangeTooltipInfo
          {
            ObjectName = treeName,
            WarningMessage = I18n.LocationNotFarm(),
            SpriteTexture = _wildTreeTexture.Value,
            SpriteSourceRect = new Rectangle(0, 0, 12, 16),
          };
        }
      }
    }

    // Placed objects (button-controlled range display)
    if (ButtonControlShow && (ButtonShowOneRange || ButtonShowAllRanges))
    {
      Vector2 gamepadTile =
        Game1.player.CurrentTool != null
          ? Utility.snapToInt(Game1.player.GetToolLocation() / Game1.tileSize)
          : Utility.snapToInt(Game1.player.GetGrabTile());
      Vector2 mouseTile = Game1.currentCursorTile;
      Vector2 tile =
        Game1.options.gamepadControls && Game1.timerUntilMouseFade <= 0 ? gamepadTile : mouseTile;
      if (Game1.currentLocation.Objects?.TryGetValue(tile, out Object? currentObject) ?? false)
      {
        if (currentObject != null)
        {
          Vector2 currentTile = Game1.GetPlacementGrabTile();
          Game1.isCheckingNonMousePlacement = !Game1.IsPerformingMousePlacement();
          Vector2 validTile =
            Utility.snapToInt(
              Utility.GetNearbyValidPlacementPosition(
                Game1.player,
                Game1.currentLocation,
                currentObject,
                (int)currentTile.X * Game1.tileSize,
                (int)currentTile.Y * Game1.tileSize
              )
            ) / Game1.tileSize;
          Game1.isCheckingNonMousePlacement = false;

          if (currentObject.Name.IndexOf("arecrow", StringComparison.OrdinalIgnoreCase) >= 0)
          {
            string itemName = currentObject.Name;
            arrayToUse = itemName.Contains("eluxe")
              ? GetDistanceArray(ObjectsWithDistance.DeluxeScarecrow, false, currentObject)
              : GetDistanceArray(ObjectsWithDistance.Scarecrow, false, currentObject);

            int tilesBeforeAdd =
              _effectiveAreaOther.Value.Count + _effectiveAreaCurrent.Value.Count;
            AddTilesToHighlightedArea(
              arrayToUse,
              !ButtonShowAllRanges,
              (int)validTile.X,
              (int)validTile.Y,
              skipNonTillable: true
            );
            int reachableTiles =
              _effectiveAreaOther.Value.Count + _effectiveAreaCurrent.Value.Count - tilesBeforeAdd;

            _rangeTooltipInfo.Value = new RangeTooltipInfo
            {
              ObjectName = ButtonShowAllRanges ? I18n.Scarecrows() : currentObject.DisplayName,
              TrackOverlap = true,
              ObjectCount = 1,
              ShowingAll = ButtonShowAllRanges,
              OccupiedTiles = 0,
              RawTotalTiles = reachableTiles,
            };
            _rangeTooltipInfo.Value.SetSpriteFromObject(currentObject);

            if (ButtonShowAllRanges)
            {
              similarObjects = GetSimilarObjectsInLocation("arecrow");
              foreach (Object next in similarObjects)
              {
                if (!next.Equals(currentObject))
                {
                  _rangeTooltipInfo.Value.ObjectCount++;
                  int[][] arrayToUse_ =
                    next.Name.IndexOf("eluxe", StringComparison.OrdinalIgnoreCase) >= 0
                      ? GetDistanceArray(ObjectsWithDistance.DeluxeScarecrow, false, next)
                      : GetDistanceArray(ObjectsWithDistance.Scarecrow, false, next);
                  tilesBeforeAdd = _effectiveAreaOther.Value.Count;
                  AddTilesToHighlightedArea(
                    arrayToUse_,
                    false,
                    (int)next.TileLocation.X,
                    (int)next.TileLocation.Y,
                    skipNonTillable: true
                  );
                  _rangeTooltipInfo.Value.RawTotalTiles +=
                    _effectiveAreaOther.Value.Count - tilesBeforeAdd;
                }
              }
            }
          }
          else if (currentObject.Name.IndexOf("sprinkler", StringComparison.OrdinalIgnoreCase) >= 0)
          {
            List<Vector2> sprinklerTilesList = currentObject.GetSprinklerTiles();

            IEnumerable<Vector2> unplacedSprinklerTiles = sprinklerTilesList;
            if (currentObject.TileLocation != validTile)
            {
              unplacedSprinklerTiles = unplacedSprinklerTiles.Select(tile =>
                tile - currentObject.TileLocation + validTile
              );
            }

            int tilesBeforeAdd = _effectiveAreaOther.Value.Count;
            AddTilesToHighlightedArea(
              unplacedSprinklerTiles,
              !ButtonShowAllRanges,
              skipNonTillable: true
            );
            int reachableTiles =
              _effectiveAreaOther.Value.Count + _effectiveAreaCurrent.Value.Count - tilesBeforeAdd;

            _rangeTooltipInfo.Value = new RangeTooltipInfo
            {
              ObjectName = ButtonShowAllRanges ? I18n.Sprinklers() : currentObject.DisplayName,
              TrackOverlap = true,
              ObjectCount = 1,
              ShowingAll = ButtonShowAllRanges,
              OccupiedTiles = 0,
              RawTotalTiles = reachableTiles,
            };
            _rangeTooltipInfo.Value.SetSpriteFromObject(currentObject);

            if (ButtonShowAllRanges)
            {
              similarObjects = GetSimilarObjectsInLocation("sprinkler");
              foreach (Object next in similarObjects)
              {
                if (!next.Equals(currentObject))
                {
                  _rangeTooltipInfo.Value.ObjectCount++;
                  tilesBeforeAdd = _effectiveAreaOther.Value.Count;
                  AddTilesToHighlightedArea(next.GetSprinklerTiles(), false, skipNonTillable: true);
                  _rangeTooltipInfo.Value.RawTotalTiles +=
                    _effectiveAreaOther.Value.Count - tilesBeforeAdd;
                }
              }
            }
          }
          else if (
            currentObject.Name.IndexOf("bee house", StringComparison.OrdinalIgnoreCase) >= 0
            || currentObject.HasContextTag("bee_house")
          )
          {
            arrayToUse = GetDistanceArray(ObjectsWithDistance.Beehouse);
            _rangeTooltipInfo.Value = new RangeTooltipInfo
            {
              ObjectName = currentObject.DisplayName,
              TrackOverlap = false,
              ObjectCount = 1,
              OccupiedTiles = 1,
              RawTotalTiles = CountTilesInArray(arrayToUse),
            };
            _rangeTooltipInfo.Value.SetSpriteFromObject(currentObject);

            AddTilesToHighlightedArea(arrayToUse, false, (int)validTile.X, (int)validTile.Y);
          }
          else if (
            currentObject.Name.IndexOf("mushroom log", StringComparison.OrdinalIgnoreCase) >= 0
          )
          {
            arrayToUse = GetDistanceArray(ObjectsWithDistance.MushroomLog);
            _rangeTooltipInfo.Value = new RangeTooltipInfo
            {
              ObjectName = currentObject.DisplayName,
              TrackOverlap = false,
              ObjectCount = 1,
              OccupiedTiles = 1,
              RawTotalTiles = CountTilesInArray(arrayToUse),
            };
            _rangeTooltipInfo.Value.SetSpriteFromObject(currentObject);

            AddTilesToHighlightedArea(arrayToUse, false, (int)validTile.X, (int)validTile.Y);
          }
          else if (
            currentObject.Name.IndexOf("mossy seed", StringComparison.OrdinalIgnoreCase) >= 0
          )
          {
            arrayToUse = GetDistanceArray(ObjectsWithDistance.MossySeed);
            _rangeTooltipInfo.Value = new RangeTooltipInfo
            {
              ObjectName = currentObject.DisplayName,
              TrackOverlap = false,
              ObjectCount = 1,
              OccupiedTiles = 1,
              RawTotalTiles = CountTilesInArray(arrayToUse),
            };
            _rangeTooltipInfo.Value.SetSpriteFromObject(currentObject);

            AddTilesToHighlightedArea(arrayToUse, false, (int)validTile.X, (int)validTile.Y);
          }
        }
      }
    }
    if (Game1.player.CurrentItem is Object currentItem && currentItem.isPlaceable())
    {
      string itemName = currentItem.Name;

      // Use the raw cursor tile for range visualization so the preview stays under the cursor
      // even when the game's valid-placement snap would jump to a distant tile (e.g. over flooring).
      Vector2 cursorTile = Utility.snapToInt(Game1.GetPlacementGrabTile());

      if (_showItemEffectRanges)
      {
        if (itemName.IndexOf("arecrow", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          arrayToUse = itemName.Contains("eluxe")
            ? GetDistanceArray(ObjectsWithDistance.DeluxeScarecrow, false, currentItem)
            : GetDistanceArray(ObjectsWithDistance.Scarecrow, false, currentItem);
          AddTilesToHighlightedArea(
            arrayToUse,
            true,
            (int)cursorTile.X,
            (int)cursorTile.Y,
            skipNonTillable: true
          );

          if (_showPlacedItemRanges)
          {
            similarObjects = GetSimilarObjectsInLocation("arecrow");
            foreach (Object next in similarObjects)
            {
              arrayToUse =
                next.Name.IndexOf("eluxe", StringComparison.OrdinalIgnoreCase) >= 0
                  ? GetDistanceArray(ObjectsWithDistance.DeluxeScarecrow, false, next)
                  : GetDistanceArray(ObjectsWithDistance.Scarecrow, false, next);
              AddTilesToHighlightedArea(
                arrayToUse,
                false,
                (int)next.TileLocation.X,
                (int)next.TileLocation.Y,
                skipNonTillable: true
              );
            }
          }
        }
        else if (itemName.IndexOf("sprinkler", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          // GetSprinklerTiles returns absolute positions in 1.6+ — offset to valid placement tile
          IEnumerable<Vector2> unplacedSprinklerTiles = currentItem.GetSprinklerTiles();
          if (currentItem.TileLocation != cursorTile)
          {
            unplacedSprinklerTiles = unplacedSprinklerTiles.Select(tile =>
              tile - currentItem.TileLocation + cursorTile
            );
          }

          AddTilesToHighlightedArea(unplacedSprinklerTiles, true, skipNonTillable: true);

          if (_showPlacedItemRanges)
          {
            similarObjects = GetSimilarObjectsInLocation("sprinkler");
            foreach (Object next in similarObjects)
            {
              AddTilesToHighlightedArea(next.GetSprinklerTiles(), false, skipNonTillable: true);
            }
          }
        }
        else if (
          itemName.IndexOf("bee house", StringComparison.OrdinalIgnoreCase) >= 0
          || currentItem.HasContextTag("bee_house")
        )
        {
          arrayToUse = GetDistanceArray(ObjectsWithDistance.Beehouse);
          AddTilesToHighlightedArea(arrayToUse, false, (int)cursorTile.X, (int)cursorTile.Y);
        }
        else if (itemName.IndexOf("mushroom log", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          arrayToUse = GetDistanceArray(ObjectsWithDistance.MushroomLog);
          AddTilesToHighlightedArea(arrayToUse, false, (int)cursorTile.X, (int)cursorTile.Y);
        }
        else if (itemName.IndexOf("mossy seed", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          arrayToUse = GetDistanceArray(ObjectsWithDistance.MossySeed);
          AddTilesToHighlightedArea(arrayToUse, false, (int)cursorTile.X, (int)cursorTile.Y);
        }
      }

      if (ShowBombRange && itemName.IndexOf("Bomb", StringComparison.OrdinalIgnoreCase) >= 0)
      {
        if (itemName.Contains("ega"))
        {
          arrayToUse = GetDistanceArray(ObjectsWithDistance.MegaBomb);
        }
        else if (itemName.Contains("herry"))
        {
          arrayToUse = GetDistanceArray(ObjectsWithDistance.CherryBomb);
        }
        else
        {
          arrayToUse = GetDistanceArray(ObjectsWithDistance.Bomb);
        }

        AddTilesToHighlightedArea(arrayToUse, false, (int)cursorTile.X, (int)cursorTile.Y);
        _isBombRange.Value = true;
      }
    }
  }

  private void AddTilesToHighlightedArea(
    IEnumerable<Vector2> tiles,
    bool overlap,
    int xPos = 0,
    int yPos = 0,
    bool skipNonTillable = false
  )
  {
    GameLocation? location = skipNonTillable ? Game1.currentLocation : null;

    // Viewport culling bounds
    xTile.Dimensions.Rectangle vp = Game1.viewport;
    int minTileX = vp.X / Game1.tileSize - 1;
    int minTileY = vp.Y / Game1.tileSize - 1;
    int maxTileX = (vp.X + vp.Width) / Game1.tileSize + 1;
    int maxTileY = (vp.Y + vp.Height) / Game1.tileSize + 1;

    foreach (Vector2 tile in tiles)
    {
      var point = tile.ToPoint();
      point.X += xPos;
      point.Y += yPos;

      if (point.X < minTileX || point.X > maxTileX || point.Y < minTileY || point.Y > maxTileY)
      {
        continue;
      }

      if (location != null)
      {
        var tileVec = new Vector2(point.X, point.Y);
        bool isTillable =
          location.doesTileHaveProperty(point.X, point.Y, "Diggable", "Back") != null
          || location.isTileHoeDirt(tileVec);
        if (!isTillable || IsTileBlocked(location, tileVec))
        {
          continue;
        }
      }

      if (overlap)
      {
        _effectiveAreaCurrent.Value.Add(point);
      }
      else
      {
        if (!_seenTiles.Value.Add(point))
        {
          _effectiveAreaIntersection.Value.Add(point);
        }

        _effectiveAreaOther.Value.Add(point);
      }
    }
  }

  private void AddTilesToHighlightedArea(
    int[][] tileMap,
    bool overlap,
    int xPos = 0,
    int yPos = 0,
    bool skipOccupied = false,
    bool skipNonTillable = false
  )
  {
    int xOffset = tileMap.Length / 2;
    GameLocation? location = (skipOccupied || skipNonTillable) ? Game1.currentLocation : null;

    // Viewport culling bounds
    xTile.Dimensions.Rectangle vp = Game1.viewport;
    int minTileX = vp.X / Game1.tileSize - 1;
    int minTileY = vp.Y / Game1.tileSize - 1;
    int maxTileX = (vp.X + vp.Width) / Game1.tileSize + 1;
    int maxTileY = (vp.Y + vp.Height) / Game1.tileSize + 1;

    for (var i = 0; i < tileMap.Length; ++i)
    {
      int yOffset = tileMap[i].Length / 2;
      for (var j = 0; j < tileMap[i].Length; ++j)
      {
        if (tileMap[i][j] == 1)
        {
          var point = new Point(xPos + i - xOffset, yPos + j - yOffset);

          if (point.X < minTileX || point.X > maxTileX || point.Y < minTileY || point.Y > maxTileY)
          {
            continue;
          }

          if (location != null)
          {
            var tileVec = new Vector2(point.X, point.Y);

            if (skipNonTillable)
            {
              bool isTillable =
                location.doesTileHaveProperty(point.X, point.Y, "Diggable", "Back") != null
                || location.isTileHoeDirt(tileVec);
              if (!isTillable || IsTileBlocked(location, tileVec))
              {
                continue;
              }
            }
            else if (skipOccupied)
            {
              if (
                location.IsTileOccupiedBy(tileVec, ignorePassables: CollisionMask.Farmers)
                || !location.isTileLocationOpen(tileVec)
              )
              {
                continue;
              }
            }
          }

          if (overlap)
          {
            _effectiveAreaCurrent.Value.Add(point);
          }
          else
          {
            if (!_seenTiles.Value.Add(point))
            {
              _effectiveAreaIntersection.Value.Add(point);
            }

            _effectiveAreaOther.Value.Add(point);
          }
        }
      }
    }
  }

  private static int CountTilesInArray(int[][] tileMap)
  {
    int count = 0;
    for (var i = 0; i < tileMap.Length; ++i)
    {
      for (var j = 0; j < tileMap[i].Length; ++j)
      {
        if (tileMap[i][j] == 1)
        {
          count++;
        }
      }
    }

    return count;
  }

  /// <summary>
  /// Checks if a tile is blocked by objects, buildings, furniture, characters, or large terrain features.
  /// Ignores farmers and HoeDirt (tilled soil with or without crops).
  /// </summary>
  private static bool IsTileBlocked(GameLocation location, Vector2 tile)
  {
    // Map layout (cliffs, built-in fences, walls on Buildings layer)
    if (!location.isTilePassable(tile))
    {
      return true;
    }

    // Objects (fences, crafted items, etc.)
    if (location.Objects.TryGetValue(tile, out Object? obj) && !obj.isPassable())
    {
      return true;
    }

    // Furniture
    if (location.GetFurnitureAt(tile) is { } furniture && !furniture.isPassable())
    {
      return true;
    }

    // Buildings
    if (location.IsTileOccupiedBy(tile, CollisionMask.Buildings))
    {
      return true;
    }

    // Terrain features - allow HoeDirt (including trellis crops), block flooring/paths and non-passable features
    if (location.terrainFeatures.TryGetValue(tile, out TerrainFeature? tf) && tf is not HoeDirt)
    {
      if (tf is Flooring || !tf.isPassable())
      {
        return true;
      }
    }

    // Large terrain features (bushes) and resource clumps
    foreach (LargeTerrainFeature ltf in location.largeTerrainFeatures)
    {
      if (
        ltf.getBoundingBox().Contains((int)tile.X * 64 + 32, (int)tile.Y * 64 + 32)
        && !ltf.isPassable()
      )
      {
        return true;
      }
    }

    foreach (ResourceClump clump in location.resourceClumps)
    {
      if (clump.occupiesTile((int)tile.X, (int)tile.Y))
      {
        return true;
      }
    }

    return false;
  }

  private List<Object> GetSimilarObjectsInLocation(string nameContains)
  {
    List<Object> result = [];

    if (!string.IsNullOrEmpty(nameContains))
    {
      OverlaidDictionary? objects = Game1.currentLocation.Objects;

      foreach (Object? nextThing in objects.Values)
      {
        if (nextThing.name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
        {
          result.Add(nextThing);
        }
      }
    }

    return result;
  }

  /// <summary>Compute intersection and exclusive areas between current and other ranges.</summary>
  private void GetOverlapValue()
  {
    if (_effectiveAreaCurrent.Value.Count == 0)
    {
      if (_rangeTooltipInfo.Value != null)
      {
        // Show-all keybind mode: overlaps already detected via _seenTiles during tile addition
        _effectiveAreaOther.Value.ExceptWith(_effectiveAreaIntersection.Value);
      }
      else
      {
        // Held-item mode but all tiles were filtered (e.g. pointing at flooring).
        // The _seenTiles intersections between placed items are not meaningful here.
        _effectiveAreaIntersection.Value.Clear();
      }

      return;
    }

    // Show-one mode: compute overlap between hovered (current) and others
    var currentSet = new HashSet<Point>(_effectiveAreaCurrent.Value);
    _effectiveAreaIntersection.Value.Clear();
    foreach (Point p in currentSet)
    {
      if (_effectiveAreaOther.Value.Contains(p))
      {
        _effectiveAreaIntersection.Value.Add(p);
      }
    }

    // Remove intersection tiles from both sets so they only render once (as overlap)
    _effectiveAreaOther.Value.ExceptWith(_effectiveAreaIntersection.Value);
    _effectiveAreaCurrent.Value.RemoveAll(_effectiveAreaIntersection.Value.Contains);
  }

  #region Distance map
  private enum ObjectsWithDistance
  {
    JunimoHut,
    Beehouse,
    Scarecrow,
    DeluxeScarecrow,
    Sprinkler,
    QualitySprinkler,
    IridiumSprinkler,
    PrismaticSprinkler,
    MushroomLog,
    MossySeed,
    WildTreeSeedSpread,
    CherryBomb,
    Bomb,
    MegaBomb,
  }

  private int[][] GetDistanceArray(
    ObjectsWithDistance type,
    bool hasPressureNozzle = false,
    Object? instance = null
  )
  {
    return type switch
    {
      ObjectsWithDistance.JunimoHut => GetCircularMask(100, maxDisplaySquareRadius: _junimoHutRadius),
      ObjectsWithDistance.Beehouse => GetCircularMask(4.19, 5, true),
      ObjectsWithDistance.Scarecrow => GetCircularMask(
        (instance?.GetRadiusForScarecrow() ?? 9) - 0.01
      ),
      ObjectsWithDistance.DeluxeScarecrow => GetCircularMask(
        (instance?.GetRadiusForScarecrow() ?? 17) - 0.01
      ),
      ObjectsWithDistance.Sprinkler => hasPressureNozzle
        ? GetCircularMask(100, maxDisplaySquareRadius: 1)
        : GetCircularMask(1),
      ObjectsWithDistance.QualitySprinkler => hasPressureNozzle
        ? GetCircularMask(100, maxDisplaySquareRadius: 2)
        : GetCircularMask(100, maxDisplaySquareRadius: 1),
      ObjectsWithDistance.IridiumSprinkler => hasPressureNozzle
        ? GetCircularMask(100, maxDisplaySquareRadius: 3)
        : GetCircularMask(100, maxDisplaySquareRadius: 2),
      ObjectsWithDistance.PrismaticSprinkler => GetCircularMask(3.69, Math.Sqrt(18), false),
      ObjectsWithDistance.MushroomLog => GetCircularMask(100, maxDisplaySquareRadius: 3),
      ObjectsWithDistance.MossySeed => GetCircularMask(100, maxDisplaySquareRadius: 2),
      ObjectsWithDistance.WildTreeSeedSpread => GetCircularMask(100, maxDisplaySquareRadius: 3),
      ObjectsWithDistance.CherryBomb => GetCircularMask(3.39),
      ObjectsWithDistance.Bomb => GetCircularMask(5.52),
      ObjectsWithDistance.MegaBomb => GetCircularMask(7.45),
      _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
  }

  private static readonly Dictionary<(double, double?, bool?, int?), int[][]> CircularMaskCache =
  [];

  private static int[][] GetCircularMask(
    double maxDistance,
    double? exceptionalDistance = null,
    bool? onlyClearExceptions = null,
    int? maxDisplaySquareRadius = null
  )
  {
    var key = (maxDistance, exceptionalDistance, onlyClearExceptions, maxDisplaySquareRadius);
    if (CircularMaskCache.TryGetValue(key, out int[][]? cached))
    {
      return cached;
    }

    int radius = Math.Max(
      (int)Math.Ceiling(maxDistance),
      exceptionalDistance.HasValue ? (int)Math.Ceiling(exceptionalDistance.Value) : 0
    );
    radius = Math.Min(
      radius,
      maxDisplaySquareRadius.HasValue ? maxDisplaySquareRadius.Value : radius
    );
    int size = 2 * radius + 1;

    var result = new int[size][];
    for (var i = 0; i < size; i++)
    {
      result[i] = new int[size];
      for (var j = 0; j < size; j++)
      {
        double distance = GetDistance(i, j, radius);
        int val =
          IsInDistance(maxDistance, distance)
          || (
            IsDistanceDirectionOK(i, j, radius, onlyClearExceptions)
            && IsExceptionalDistanceOK(exceptionalDistance, distance)
          )
            ? 1
            : 0;
        result[i][j] = val;
      }
    }

    CircularMaskCache[key] = result;
    return result;
  }

  private static bool IsDistanceDirectionOK(int i, int j, int radius, bool? onlyClearExceptions)
  {
    return onlyClearExceptions.HasValue && onlyClearExceptions.Value
      ? radius - j == 0 || radius - i == 0
      : true;
  }

  private static bool IsExceptionalDistanceOK(double? exceptionalDistance, double distance)
  {
    return exceptionalDistance.HasValue && exceptionalDistance.Value == distance;
  }

  private static bool IsInDistance(double maxDistance, double distance)
  {
    return distance <= maxDistance;
  }

  private static double GetDistance(int i, int j, int radius)
  {
    return Math.Sqrt((radius - i) * (radius - i) + (radius - j) * (radius - j));
  }
  #endregion
  #endregion
}
