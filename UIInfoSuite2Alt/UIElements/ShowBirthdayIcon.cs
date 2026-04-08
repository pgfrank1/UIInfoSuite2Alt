using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using UIInfoSuite2Alt.Infrastructure;
using UIInfoSuite2Alt.Infrastructure.Extensions;

namespace UIInfoSuite2Alt.UIElements;

internal class ShowBirthdayIcon : IDisposable
{
  #region Properties
  private readonly PerScreen<List<NPC>> _birthdayNPCs = new(() => []);

  private readonly PerScreen<List<ClickableTextureComponent>> _birthdayIcons = new(() => []);

  private bool Enabled { get; set; }
  private bool HideBirthdayIfFullFriendShip { get; set; }
  private bool UseStackedBirthdayIcons { get; set; }
  private readonly IModHelper _helper;
  #endregion


  #region Life cycle
  public ShowBirthdayIcon(IModHelper helper)
  {
    _helper = helper;
  }

  public void Dispose()
  {
    ToggleOption(false);
  }

  public void ToggleOption(bool showBirthdayIcon)
  {
    Enabled = showBirthdayIcon;

    _helper.Events.GameLoop.DayStarted -= OnDayStarted;
    _helper.Events.Display.RenderingHud -= OnRenderingHud;
    _helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;

    if (showBirthdayIcon)
    {
      CheckForBirthday();
      _helper.Events.GameLoop.DayStarted += OnDayStarted;
      _helper.Events.Display.RenderingHud += OnRenderingHud;
      _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }
  }

  public void ToggleDisableOnMaxFriendshipOption(bool hideBirthdayIfFullFriendShip)
  {
    HideBirthdayIfFullFriendShip = hideBirthdayIfFullFriendShip;
    ToggleOption(Enabled);
  }

  public void ToggleStackedOption(bool useStackedBirthdayIcons)
  {
    UseStackedBirthdayIcons = useStackedBirthdayIcons;
    _birthdayIcons.Value.Clear();
  }
  #endregion


  #region Event subscriptions
  private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
  {
    if (e.IsOneSecond)
    {
      CheckForGiftGiven();
    }
  }

  private void OnDayStarted(object? sender, DayStartedEventArgs e)
  {
    CheckForBirthday();
  }

  private void OnRenderingHud(object? sender, RenderingHudEventArgs e)
  {
    if (UIElementUtils.IsRenderingNormally())
    {
      EnqueueBirthdayIcons();
    }
  }
  #endregion


  #region Logic
  private void CheckForGiftGiven()
  {
    List<NPC> npcs = _birthdayNPCs.Value;
    // Iterate from the end so that removing items doesn't affect indices
    for (int i = npcs.Count - 1; i >= 0; i--)
    {
      Friendship? friendship = GetFriendshipWithNPC(npcs[i].Name);
      if (friendship != null && friendship.GiftsToday > 0)
      {
        npcs.RemoveAt(i);
        _birthdayIcons.Value.Clear();
      }
    }
  }

  private void CheckForBirthday()
  {
    _birthdayNPCs.Value.Clear();
    _birthdayIcons.Value.Clear();
    HashSet<string> seen = new();
    foreach (GameLocation? location in Game1.locations)
    {
      foreach (NPC? character in location.characters)
      {
        if (character.isBirthday() && seen.Add(character.Name))
        {
          Friendship? friendship = GetFriendshipWithNPC(character.Name);
          if (friendship != null)
          {
            if (
              HideBirthdayIfFullFriendShip
              && friendship.Points
                >= Utility.GetMaximumHeartsForCharacter(character)
                  * NPC.friendshipPointsPerHeartLevel
            )
            {
              continue;
            }

            _birthdayNPCs.Value.Add(character);
          }
        }
      }
    }

    if (_birthdayNPCs.Value.Count > 0)
    {
      ModEntry.MonitorObject.LogOnce(
        $"ShowBirthdayIcon: birthdays today, npcs=[{string.Join(", ", _birthdayNPCs.Value.Select(n => n.Name))}]",
        LogLevel.Trace
      );
    }
  }

  private static Friendship? GetFriendshipWithNPC(string name)
  {
    try
    {
      if (Game1.player.friendshipData.TryGetValue(name, out Friendship friendship))
      {
        return friendship;
      }

      return null;
    }
    catch (Exception ex)
    {
      ModEntry.MonitorObject.LogOnce(
        $"ShowBirthdayIcon: failed to get friendship data, npc={name}",
        LogLevel.Error
      );
      ModEntry.MonitorObject.Log(ex.ToString());
    }

    return null;
  }

  private static readonly Rectangle BirthdayBackgroundSource = new(228, 409, 16, 16);
  private const float IconScale = 2.9f;
  private const float HeadshotScale = 2f;
  private const int HeadshotSize = 16;

  private void EnqueueBirthdayIcons()
  {
    List<NPC> npcs = _birthdayNPCs.Value;
    if (npcs.Count == 0)
    {
      return;
    }

    List<ClickableTextureComponent> icons = _birthdayIcons.Value;

    // Use stacked mode only when enabled AND 2+ NPCs have birthdays
    if (UseStackedBirthdayIcons && npcs.Count >= 2)
    {
      EnqueueStackedIcon(npcs, icons);
    }
    else
    {
      EnqueueIndividualIcons(npcs, icons);
    }
  }

  private static void EnqueueIndividualIcons(List<NPC> npcs, List<ClickableTextureComponent> icons)
  {
    // Rebuild icon list only when NPC count changes
    if (icons.Count != npcs.Count)
    {
      icons.Clear();
      foreach (NPC npc in npcs)
      {
        icons.Add(
          new ClickableTextureComponent(
            npc.Name,
            Rectangle.Empty,
            null,
            npc.Name,
            npc.Sprite.Texture,
            npc.GetHeadShot(),
            HeadshotScale
          )
        );
      }
    }

    for (int i = 0; i < npcs.Count; i++)
    {
      int capturedI = i;
      IconHandler.Handler.EnqueueIcon(
        "Birthday",
        (batch, pos) =>
        {
          DrawBirthdayBackground(batch, pos);
          icons[capturedI].bounds = GetIconBounds(pos);
          icons[capturedI].sourceRect = npcs[capturedI].GetHeadShot();
          icons[capturedI].draw(batch);
        },
        batch =>
        {
          if (icons[capturedI].containsPoint(Game1.getMouseX(), Game1.getMouseY()))
          {
            string hoverText = string.Format(I18n.NpcBirthday(), npcs[capturedI].displayName);
            IClickableMenu.drawHoverText(batch, hoverText, Game1.smallFont);
          }
        }
      );
    }
  }

  private static void EnqueueStackedIcon(List<NPC> npcs, List<ClickableTextureComponent> icons)
  {
    // Use a single icon entry for the stacked view
    if (icons.Count != 1 || icons[0].name != "Stacked")
    {
      icons.Clear();
      icons.Add(
        new ClickableTextureComponent(
          "Stacked",
          Rectangle.Empty,
          null,
          "Stacked",
          Game1.mouseCursors,
          BirthdayBackgroundSource,
          IconScale
        )
      );
    }

    IconHandler.Handler.EnqueueIcon(
      "Birthday",
      (batch, pos) =>
      {
        DrawBirthdayBackground(batch, pos);

        // Draw digit count centered on the birthday icon
        int count = npcs.Count;
        int digitCount = count == 0 ? 1 : (int)Math.Floor(Math.Log10(count)) + 1;
        int totalDigitWidth = digitCount * Tools.TinyDigitStep;
        float iconCenterX = pos.X + 16f * IconScale / 2f;
        float iconCenterY = pos.Y - 3f + 20f * IconScale / 2f;
        float xOffset = 0f;
        var digitPos = new Vector2(iconCenterX - totalDigitWidth / 2f, iconCenterY - 7f);
        Tools.DrawTinyDigits(
          batch,
          count,
          digitPos,
          ref xOffset,
          Tools.TinyDigitStep,
          Color.White,
          Color.Black * 0.3f
        );

        icons[0].bounds = GetIconBounds(pos);
      },
      batch =>
      {
        if (icons[0].containsPoint(Game1.getMouseX(), Game1.getMouseY()))
        {
          DrawStackedTooltip(batch, npcs);
        }
      }
    );
  }

  private static void DrawBirthdayBackground(SpriteBatch batch, Point pos)
  {
    batch.Draw(
      Game1.mouseCursors,
      new Vector2(pos.X, pos.Y - 3),
      BirthdayBackgroundSource,
      Color.White,
      0.0f,
      Vector2.Zero,
      IconScale,
      SpriteEffects.None,
      1f
    );
  }

  private static Rectangle GetIconBounds(Point pos)
  {
    return new Rectangle(pos.X - 7, pos.Y - 5, (int)(16.0 * IconScale), (int)(16.0 * IconScale));
  }

  private static void DrawStackedTooltip(SpriteBatch batch, List<NPC> npcs)
  {
    SpriteFont font = Game1.smallFont;
    string header = I18n.BirthdaysToday();

    // Measure tooltip dimensions
    float headshotDrawSize = HeadshotSize * HeadshotScale;
    float headshotPadding = 4f;
    float lineHeight = Math.Max(font.LineSpacing, headshotDrawSize);
    float headerWidth = font.MeasureString(header).X;
    float maxNameWidth = 0f;
    foreach (NPC npc in npcs)
    {
      float nameWidth = font.MeasureString(npc.displayName).X;
      if (nameWidth > maxNameWidth)
      {
        maxNameWidth = nameWidth;
      }
    }

    float npcLineWidth = headshotDrawSize + headshotPadding + maxNameWidth;
    float contentWidth = Math.Max(headerWidth, npcLineWidth);
    int padding = 16;
    int tooltipWidth = (int)contentWidth + padding * 2;
    int tooltipHeight = (int)(font.LineSpacing + 4 + npcs.Count * lineHeight) + padding * 2;

    // Position tooltip near mouse
    int mouseX = Game1.getMouseX();
    int mouseY = Game1.getMouseY();
    int x = mouseX + 32;
    int y = mouseY + 32;
    Rectangle safeArea = Utility.getSafeArea();
    if (x + tooltipWidth > safeArea.Right)
    {
      x = safeArea.Right - tooltipWidth;
    }

    if (y + tooltipHeight > safeArea.Bottom)
    {
      y = safeArea.Bottom - tooltipHeight;
    }

    // Draw tooltip background
    IClickableMenu.drawTextureBox(
      batch,
      Game1.menuTexture,
      new Rectangle(0, 256, 60, 60),
      x,
      y,
      tooltipWidth,
      tooltipHeight,
      Color.White
    );

    // Draw header
    float textX = x + padding;
    float textY = y + padding;
    Utility.drawTextWithShadow(batch, header, font, new Vector2(textX, textY), Game1.textColor);
    textY += font.LineSpacing + 4;

    // Draw each NPC line: headshot + name
    foreach (NPC npc in npcs)
    {
      Rectangle headShot = npc.GetHeadShot();
      float scale = headshotDrawSize / headShot.Width;
      float headshotY = textY + (lineHeight - headShot.Height * scale) / 2f - 10f;

      batch.Draw(
        npc.Sprite.Texture,
        new Vector2(textX, headshotY),
        headShot,
        Color.White,
        0f,
        Vector2.Zero,
        scale,
        SpriteEffects.None,
        1f
      );

      float nameY = textY + (lineHeight - font.LineSpacing) / 2f;
      Utility.drawTextWithShadow(
        batch,
        npc.displayName,
        font,
        new Vector2(textX + headshotDrawSize + headshotPadding, nameY),
        Game1.textColor
      );

      textY += lineHeight;
    }
  }
  #endregion
}
