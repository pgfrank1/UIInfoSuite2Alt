using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace UIInfoSuite2Alt.Patches;

internal class ShowAccurateHearts : IDisposable
{
  #region Properties
  private static bool _enabled;

  // Filled heart sprite on Game1.mouseCursors: 7x6 source pixels at 4x scale = 28x24 on screen.
  private const int HeartSourceX = 211;
  private const int HeartSourceY = 428;
  private const int HeartSourceWidth = 7;
  private const int HeartSourceHeight = 6;
  private const int HeartScale = 4;
  #endregion

  #region Lifecycle
  public static void Initialize(Harmony harmony)
  {
    harmony.Patch(
      original: AccessTools.Method(
        typeof(SocialPage),
        nameof(SocialPage.draw),
        [typeof(SpriteBatch)]
      ),
      postfix: new HarmonyMethod(typeof(ShowAccurateHearts), nameof(AfterSocialPageDraw))
    );

    ModEntry.MonitorObject.Log("ShowAccurateHearts: Harmony patch applied", LogLevel.Trace);
  }

  public void Dispose()
  {
    ToggleOption(false);
  }

  public void ToggleOption(bool showAccurateHearts)
  {
    _enabled = showAccurateHearts;
  }
  #endregion

  #region Harmony patch
  private static void AfterSocialPageDraw(SocialPage __instance)
  {
    if (!_enabled)
    {
      return;
    }

    DrawHeartFills(__instance);
  }
  #endregion

  #region Logic
  private static void DrawHeartFills(SocialPage socialPage)
  {
    for (
      int i = socialPage.slotPosition;
      i < socialPage.slotPosition + 5 && i < socialPage.SocialEntries.Count;
      ++i
    )
    {
      string internalName = socialPage.SocialEntries[i].InternalName;
      NPC? npc = Game1.getCharacterFromName(internalName);
      int maxHearts = npc != null ? Utility.GetMaximumHeartsForCharacter(npc) : 10;
      if (
        Game1.player.friendshipData.TryGetValue(internalName, out Friendship friendshipValues)
        && friendshipValues.Points > 0
        && friendshipValues.Points < maxHearts * 250
      )
      {
        int pointsToNextHeart = friendshipValues.Points % 250;
        int numHearts = friendshipValues.Points / 250;
        DrawPartialHeart(socialPage, i, numHearts, pointsToNextHeart);
      }
    }
  }

  /// <summary>
  /// Draws the filled heart sprite cropped from bottom to top based on friendship progress.
  /// Uses Game1.mouseCursors so Content Patcher recolors (e.g. Cat Valley) are respected.
  /// </summary>
  private static void DrawPartialHeart(
    SocialPage socialPage,
    int slotIndex,
    int heartLevel,
    int friendshipPoints
  )
  {
    // Match game's heart positioning from SocialPage.drawNPCSlotHeart
    int heartIndex = heartLevel < 10 ? heartLevel : heartLevel - 10;
    int heartX = socialPage.xPositionOnScreen + 320 - 4 + heartIndex * 32;
    int heartY = socialPage.sprites[slotIndex].bounds.Y + (heartLevel < 10 ? 64 - 28 : 64);

    // Fill from bottom up at screen pixel granularity (24 steps per heart).
    // Split into complete source rows and a partial top row.
    int totalHeight = HeartSourceHeight * HeartScale; // 24 screen pixels
    int fillHeight = (int)Math.Ceiling((double)friendshipPoints / 250 * totalHeight);
    int completeRows = fillHeight / HeartScale;
    int partialPixels = fillHeight % HeartScale;
    var tint = Color.White * 0.7f;

    // Draw complete source rows from the bottom
    if (completeRows > 0)
    {
      int srcY = HeartSourceY + HeartSourceHeight - completeRows;
      int dstY = heartY + (HeartSourceHeight - completeRows) * HeartScale;
      Game1.spriteBatch.Draw(
        Game1.mouseCursors,
        new Rectangle(heartX, dstY, HeartSourceWidth * HeartScale, completeRows * HeartScale),
        new Rectangle(HeartSourceX, srcY, HeartSourceWidth, completeRows),
        tint
      );
    }

    // Draw partial top row (1 source row clipped to partialPixels height)
    if (partialPixels > 0)
    {
      int srcRow = HeartSourceHeight - completeRows - 1;
      int srcY = HeartSourceY + srcRow;
      int dstY = heartY + srcRow * HeartScale + (HeartScale - partialPixels);
      Game1.spriteBatch.Draw(
        Game1.mouseCursors,
        new Rectangle(heartX, dstY, HeartSourceWidth * HeartScale, partialPixels),
        new Rectangle(HeartSourceX, srcY, HeartSourceWidth, 1),
        tint
      );
    }
  }
  #endregion
}
