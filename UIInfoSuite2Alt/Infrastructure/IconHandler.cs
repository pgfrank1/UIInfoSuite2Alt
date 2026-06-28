using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using UIInfoSuite2Alt.UIElements;

namespace UIInfoSuite2Alt.Infrastructure;

public sealed class IconHandler
{
  public static readonly string[] IconKeys =
  [
    "Luck",
    "Weather",
    "Birthday",
    "Festival",
    "QueenOfSauce",
    "ToolUpgrade",
    "RobinBuilding",
    "SeasonalBerry",
    "TravelingMerchant",
    "Bookseller",
    "CustomIcons",
  ];

  private const int IconGap = 48;

  /// <summary>How many icons per row (horizontal) or column (vertical) before wrapping.</summary>
  public int IconsPerRow { get; set; } = 10;

  private readonly PerScreen<List<QueuedIcon>> _queuedIcons = new(() => []);
  private readonly PerScreen<List<QueuedIcon>> _sortedCache = new(() => []);
  private readonly PerScreen<int> _lastSortedCount = new(() => -1);

  private IconHandler() { }

  public static IconHandler Handler { get; } = new();

  public bool IsQuestLogPermanent { get; set; } = false;

  /// <summary>When true, a quest count number is drawn below the journal icon, requiring more vertical clearance.</summary>
  public bool ShowQuestCount { get; set; } = true;

  /// <summary>The configured icon order, keyed by icon key. Lower = more right.</summary>
  public Dictionary<string, int> IconOrder { get; set; } = [];

  /// <summary>Extra vertical offset (pixels) to avoid overlapping icons from other mods.</summary>
  public int ExtraYOffset { get; set; }

  /// <summary>Extra horizontal offset (pixels); negative shifts the icon row left. Used for Launcher Drawer.</summary>
  public int ExtraXOffset { get; set; }

  /// <summary>When true, icons stack vertically downward instead of horizontally to the left.</summary>
  public bool UseVerticalLayout { get; set; }

  /// <summary>Enqueue an icon to draw this frame, sorted by configured order.</summary>
  public void EnqueueIcon(
    string iconKey,
    Action<SpriteBatch, Point> draw,
    Action<SpriteBatch>? drawHover = null
  )
  {
    int order = IconOrder.TryGetValue(iconKey, out int o) ? o : 99;
    _queuedIcons.Value.Add(
      new QueuedIcon
      {
        Draw = draw,
        DrawHover = drawHover,
        SortOrder = order,
        RegistrationOrder = _queuedIcons.Value.Count,
      }
    );
  }

  /// <summary>Sort, position, draw all queued icons + hover text. Call once per frame.</summary>
  public void DrawQueuedIcons(SpriteBatch batch)
  {
    List<QueuedIcon> icons = _queuedIcons.Value;
    if (icons.Count == 0)
    {
      return;
    }

    // Skip when HUD is hidden (cutscenes, events)
    if (!UIElementUtils.IsRenderingNormally())
    {
      icons.Clear();
      return;
    }

    // Stable sort: config order, then registration order. The sorted list instance is reused
    // across frames to avoid reallocating it, but contents are rebuilt every frame: icons are
    // re-enqueued each frame and _lastSortedCount is reset to -1 below, so the count guard always
    // re-sorts. (The guard is vestigial - kept only because it is harmless.)
    List<QueuedIcon> sorted = _sortedCache.Value;
    if (_lastSortedCount.Value != icons.Count)
    {
      sorted.Clear();
      sorted.AddRange(icons.OrderBy(i => i.SortOrder).ThenBy(i => i.RegistrationOrder));
      _lastSortedCount.Value = icons.Count;
    }

    int yPos = (Game1.options.zoomButtons ? 290 : 260) + ExtraYOffset;
    int xBase = Tools.GetWidthInPlayArea() - 70 + ExtraXOffset;

    if (IsQuestLogPermanent || Game1.player.hasVisibleQuests)
    {
      if (UseVerticalLayout)
      {
        xBase -= 16;
        yPos += ShowQuestCount ? 50 : 20;
      }
      else
      {
        xBase -= 67;
      }
    }
    else if (UseVerticalLayout)
    {
      yPos -= 30;
    }

    // Draw icons with fixed spacing, wrapping after IconsPerRow.
    // Clamp to >= 1: the UI constrains this to 1-10, but config.json is hand-editable and a 0
    // would throw DivideByZeroException below on every frame.
    int perRow = Math.Max(1, IconsPerRow);
    for (int i = 0; i < sorted.Count; i++)
    {
      int col = i % perRow;
      int row = i / perRow;
      Point pos = UseVerticalLayout
        ? new Point(xBase - IconGap * row, yPos + IconGap * col)
        : new Point(xBase - IconGap * col, yPos + IconGap * row);
      sorted[i].Draw(batch, pos);
    }

    // Draw hover text on top
    foreach (QueuedIcon icon in sorted)
    {
      icon.DrawHover?.Invoke(batch);
    }

    icons.Clear();
    _lastSortedCount.Value = -1;
  }

  public void Reset(object? sender, EventArgs e)
  {
    _queuedIcons.Value.Clear();
    _lastSortedCount.Value = -1;
  }

  private class QueuedIcon
  {
    public Action<SpriteBatch, Point> Draw { get; set; } = null!;
    public Action<SpriteBatch>? DrawHover { get; set; }
    public int SortOrder { get; set; }
    public int RegistrationOrder { get; set; }
  }
}
