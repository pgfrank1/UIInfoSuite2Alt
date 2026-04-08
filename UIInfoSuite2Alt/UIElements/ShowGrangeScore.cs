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

  // Placement tiers: checked in order, first match wins. Prize values from Event.cs dialogue rewards.
  private static readonly (
    int minScore,
    Func<string> label,
    Func<Color> color,
    int prize
  )[] PlacementTiers =
  [
    (90, I18n.GrangeScore_FirstPlace, () => Tools.TooltipGreen, 1000),
    (75, I18n.GrangeScore_SecondPlace, () => Tools.TooltipYellow, 500),
    (60, I18n.GrangeScore_ThirdPlace, () => Tools.TooltipYellow, 250),
    (int.MinValue, I18n.GrangeScore_FourthPlace, () => Tools.TooltipRed, 50),
  ];

  // Star token icon from Cursors spritesheet (same icon the game uses for the fair score HUD)
  private static readonly Rectangle StarTokenSourceRect = new(338, 400, 8, 8);
  private const float StarTokenScale = 3f;
  private const int DisqualifiedPrize = 750;

  private readonly IModHelper _helper;

  public bool ShowPrize { get; set; } = true;

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
    DrawScoreOverlay(e.SpriteBatch, container, score, hasMayorShorts, ShowPrize);

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
    bool hasMayorShorts,
    bool showPrize
  )
  {
    string scoreText;
    string placementText;
    Color scoreColor;
    int prize;

    if (hasMayorShorts)
    {
      scoreText = "???";
      placementText = I18n.GrangeScore_Disqualified();
      scoreColor = Color.DimGray;
      prize = DisqualifiedPrize;
    }
    else
    {
      scoreText = score.ToString();
      (_, Func<string> label, Func<Color> color, int tierPrize) = Array.Find(
        PlacementTiers,
        t => score >= t.minScore
      );
      placementText = label();
      scoreColor = color();
      prize = tierPrize;
    }

    // "Score: {score}/90 ({placement}) - Prize: [icon] amount"
    // Only the score number and placement text are colored; everything else is default.
    SpriteFont font = Game1.smallFont;
    string labelText = I18n.GrangeScore_Label();
    string slashMax = "/90 (";
    string closeParenOnly = ")";
    string prizeLabel = $" - {I18n.GrangeScore_Prize()}";
    string prizeAmount = $"{prize:N0}";

    int starTokenSize = (int)(StarTokenSourceRect.Width * StarTokenScale);
    int iconSpacing = 4;

    // Measure text width: label + score + /90( + placement + )
    float totalTextWidth =
      font.MeasureString(labelText).X
      + font.MeasureString(scoreText).X
      + font.MeasureString(slashMax).X
      + font.MeasureString(placementText).X
      + font.MeasureString(closeParenOnly).X;

    if (showPrize)
    {
      totalTextWidth +=
        font.MeasureString(prizeLabel).X
        + starTokenSize
        + iconSpacing * 2
        + font.MeasureString(prizeAmount).X;
    }

    int paddingX = 16;
    int paddingY = 12;
    int boxWidth = (int)totalTextWidth + paddingX * 2;
    int boxHeight = (int)font.MeasureString(labelText).Y + paddingY * 2;

    // Center above the StorageContainer menu
    int boxX = container.xPositionOnScreen + container.width / 2 - boxWidth / 2;
    int boxY = container.yPositionOnScreen - boxHeight - 16;

    Game1.DrawBox(boxX, boxY, boxWidth, boxHeight);

    float cursorX = boxX + paddingX;
    int textY = boxY + paddingY;

    // "Score: " - default
    Utility.drawTextWithShadow(
      batch,
      labelText,
      font,
      new Vector2(cursorX, textY),
      Game1.textColor
    );
    cursorX += font.MeasureString(labelText).X;

    // "XX" - colored
    Utility.drawTextWithShadow(batch, scoreText, font, new Vector2(cursorX, textY), scoreColor);
    cursorX += font.MeasureString(scoreText).X;

    // "/90 (" - default
    Utility.drawTextWithShadow(batch, slashMax, font, new Vector2(cursorX, textY), Game1.textColor);
    cursorX += font.MeasureString(slashMax).X;

    // "1st Place" - colored
    Utility.drawTextWithShadow(batch, placementText, font, new Vector2(cursorX, textY), scoreColor);
    cursorX += font.MeasureString(placementText).X;

    // ")" - default
    Utility.drawTextWithShadow(
      batch,
      closeParenOnly,
      font,
      new Vector2(cursorX, textY),
      Game1.textColor
    );
    cursorX += font.MeasureString(closeParenOnly).X;

    if (!showPrize)
    {
      return;
    }

    // " - Prize: " - default
    Utility.drawTextWithShadow(
      batch,
      prizeLabel,
      font,
      new Vector2(cursorX, textY),
      Game1.textColor
    );
    cursorX += font.MeasureString(prizeLabel).X;

    // Star token icon, vertically centered with text, nudged up 2px
    cursorX += iconSpacing;
    float iconY = textY + (font.MeasureString(prizeAmount).Y - starTokenSize) / 2f - 2;
    batch.Draw(
      Game1.mouseCursors,
      new Vector2(cursorX, iconY),
      StarTokenSourceRect,
      Color.White,
      0f,
      Vector2.Zero,
      StarTokenScale,
      SpriteEffects.None,
      1f
    );
    cursorX += starTokenSize + iconSpacing;

    // "1,000" - default
    Utility.drawTextWithShadow(
      batch,
      prizeAmount,
      font,
      new Vector2(cursorX, textY),
      Game1.textColor
    );
  }
}
