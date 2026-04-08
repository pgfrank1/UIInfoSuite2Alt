using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using UIInfoSuite2Alt.Infrastructure;
using SObject = StardewValley.Object;

namespace UIInfoSuite2Alt.UIElements;

/// <summary>Shows a live score overlay above the grange display StorageContainer</summary>
internal class ShowGrangeScore : IDisposable
{
  // Price thresholds: (minPrice, maxQuality) - item scores +1 for each threshold met
  private static readonly (int price, int maxQuality)[] PriceTiers =
  [
    (20, int.MaxValue),
    (90, int.MaxValue),
    (200, int.MaxValue),
    (300, 1), // quality must be < 2 (not gold/iridium)
    (400, 0), // quality must be < 1 (normal only)
  ];

  // Category -> bit flag mapping for diversity scoring
  private static readonly (int[] categories, int bit)[] CategoryGroups =
  [
    ([-75], 1), // Vegetables
    ([-79], 2), // Fruits
    ([-18, -14, -6, -5], 4), // Animal products
    ([-12, -2], 8), // Minerals/Gems
    ([-4], 16), // Fish
    ([-81, -80, -27], 32), // Greens/Flowers/Forage
    ([-7], 64), // Cooking
    ([-26], 128), // Artisan
  ];

  // Placement tiers: checked in order, first match wins
  private static readonly (int minScore, Func<string> label, Func<Color> color)[] PlacementTiers =
  [
    (90, I18n.GrangeScore_FirstPlace, () => Tools.TooltipGreen),
    (75, I18n.GrangeScore_SecondPlace, () => Tools.TooltipYellow),
    (60, I18n.GrangeScore_ThirdPlace, () => Tools.TooltipYellow),
    (int.MinValue, I18n.GrangeScore_FourthPlace, () => Tools.TooltipRed),
  ];

  private readonly IModHelper _helper;

  public ShowGrangeScore(IModHelper helper)
  {
    _helper = helper;
  }

  public void Dispose()
  {
    ToggleOption(false);
  }

  public void ToggleOption(bool enabled)
  {
    _helper.Events.Display.RenderedActiveMenu -= OnRenderedActiveMenu;

    if (enabled)
    {
      _helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
    }
  }

  private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
  {
    if (Game1.activeClickableMenu is not StorageContainer container)
    {
      return;
    }

    if (Game1.CurrentEvent is not { } currentEvent || !currentEvent.isSpecificFestival("fall16"))
    {
      return;
    }

    int score = CalculateGrangeScore(out bool hasMayorShorts);
    DrawScoreOverlay(e.SpriteBatch, container, score, hasMayorShorts);

    // Redraw mouse cursor on top so the score overlay doesn't cover it
    container.drawMouse(e.SpriteBatch);
  }

  /// <summary>Replicates the scoring logic from Event.judgeGrange()</summary>
  private static int CalculateGrangeScore(out bool hasMayorShorts)
  {
    hasMayorShorts = false;
    int score = 14;
    int emptySlots = 0;
    int categoryFlags = 0;

    foreach (Item? item in Game1.player.team.grangeDisplay)
    {
      if (item is SObject obj)
      {
        if (obj.QualifiedItemId is "(O)789" or "(O)71")
        {
          hasMayorShorts = true;
        }

        score += obj.Quality + 1;

        int sellPrice = obj.sellToStorePrice(-1L);
        foreach ((int price, int maxQuality) in PriceTiers)
        {
          if (sellPrice >= price && obj.Quality < maxQuality)
          {
            score++;
          }
        }

        foreach ((int[] categories, int bit) in CategoryGroups)
        {
          if (Array.IndexOf(categories, obj.Category) >= 0)
          {
            categoryFlags |= bit;
            break;
          }
        }
      }
      else if (item == null)
      {
        emptySlots++;
      }
    }

    // Count set bits (unique categories) via Brian Kernighan's algorithm
    int flags = categoryFlags;
    int categoryCount = 0;
    while (flags != 0)
    {
      categoryCount++;
      flags &= flags - 1;
    }

    score += Math.Min(30, categoryCount * 5);
    score += 9 - 2 * emptySlots;

    return score;
  }

  private static void DrawScoreOverlay(
    SpriteBatch batch,
    StorageContainer container,
    int score,
    bool hasMayorShorts
  )
  {
    string scoreText;
    string placementText;
    Color scoreColor;

    if (hasMayorShorts)
    {
      scoreText = "???";
      placementText = I18n.GrangeScore_Disqualified();
      scoreColor = Color.DimGray;
    }
    else
    {
      scoreText = score.ToString();
      (_, Func<string> label, Func<Color> color) = Array.Find(
        PlacementTiers,
        t => score >= t.minScore
      );
      placementText = label();
      scoreColor = color();
    }

    // "Score: XX - Placement"
    string labelText = I18n.GrangeScore_Label();
    string fullText = $"{labelText}{scoreText} - {placementText}";

    SpriteFont font = Game1.smallFont;
    Vector2 textSize = font.MeasureString(fullText);

    int paddingX = 16;
    int paddingY = 12;
    int boxWidth = (int)textSize.X + paddingX * 2;
    int boxHeight = (int)textSize.Y + paddingY * 2;

    // Center above the StorageContainer menu
    int boxX = container.xPositionOnScreen + container.width / 2 - boxWidth / 2;
    int boxY = container.yPositionOnScreen - boxHeight - 16;

    Game1.DrawBox(boxX, boxY, boxWidth, boxHeight);

    int textX = boxX + paddingX;
    int textY = boxY + paddingY;

    // Draw score label ("Score: ") in default color
    float labelWidth = font.MeasureString(labelText).X;
    Utility.drawTextWithShadow(batch, labelText, font, new Vector2(textX, textY), Game1.textColor);

    // Draw score value + placement in tier color
    string coloredPart = $"{scoreText} - {placementText}";
    Utility.drawTextWithShadow(
      batch,
      coloredPart,
      font,
      new Vector2(textX + labelWidth, textY),
      scoreColor
    );
  }
}
