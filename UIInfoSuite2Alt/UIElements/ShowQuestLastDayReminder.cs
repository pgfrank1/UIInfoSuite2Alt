using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using UIInfoSuite2Alt.Infrastructure;

namespace UIInfoSuite2Alt.UIElements;

internal class ShowQuestLastDayReminder : IDisposable
{
  #region Properties
  // Clock glyph on mouseCursors, drawn over the quest journal icon.
  private static readonly Rectangle ClockSourceRect = new(434, 475, 9, 9);
  private const float ClockScale = 3f;

  // Slow color flash between normal and a red tint (~2.5s full cycle at this period).
  private static readonly Color FlashColor = new(255, 100, 100);
  private const double FlashPeriodMs = 400.0;

  private readonly IModHelper _helper;
  #endregion

  #region Lifecycle
  public ShowQuestLastDayReminder(IModHelper helper)
  {
    _helper = helper;
  }

  public void Dispose()
  {
    ToggleOption(false);
  }

  public void ToggleOption(bool showReminder)
  {
    _helper.Events.Display.RenderedHud -= OnRenderedHud;

    if (showReminder)
    {
      _helper.Events.Display.RenderedHud += OnRenderedHud;
    }
  }
  #endregion

  #region Event subscriptions
  // Draw after the HUD so the clock renders on top of the quest journal icon.
  private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
  {
    if (
      !UIElementUtils.IsRenderingNormally()
      || !Game1.player.hasVisibleQuests
      || !HasLastDayQuest()
    )
    {
      return;
    }

    // Center the clock over the quest journal icon (offsets tuned against questButton.bounds), then
    // snap to a whole-pixel top-left (origin Vector2.Zero, integer scale) so every source pixel maps
    // to exactly ClockScale screen pixels - i.e. pixel perfect under PointClamp.
    Rectangle journal = Game1.dayTimeMoneyBox.questButton.bounds;
    float half = ClockSourceRect.Width * ClockScale / 2f;
    var clockPos = new Vector2(
      (int)Math.Round(journal.X + 46 - half),
      (int)Math.Round(journal.Y + 23 - half)
    );

    float amount = (float)(
      (Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / FlashPeriodMs) + 1) / 2
    );
    Color color = Color.Lerp(Color.White, FlashColor, amount);

    // Shadow behind the clock.
    Game1.spriteBatch.Draw(
      Game1.mouseCursors,
      clockPos + new Vector2(0, 3),
      ClockSourceRect,
      Color.White * 0.3f,
      0f,
      Vector2.Zero,
      ClockScale,
      Microsoft.Xna.Framework.Graphics.SpriteEffects.None,
      0.98f
    );

    Game1.spriteBatch.Draw(
      Game1.mouseCursors,
      clockPos,
      ClockSourceRect,
      color,
      0f,
      Vector2.Zero,
      ClockScale,
      Microsoft.Xna.Framework.Graphics.SpriteEffects.None,
      0.99f
    );
  }
  #endregion

  #region Logic
  // Last day = value == 1 for both systems: journal quests decrement daysLeft at night and are
  // removed when <= 0; special orders are removed at newDay when GetDaysLeft() <= 0.
  private static bool HasLastDayQuest()
  {
    bool journalLastDay = Game1.player.questLog.Any(q =>
      q != null && q.IsTimedQuest() && q.daysLeft.Value == 1 && !q.IsHidden()
    );

    bool specialOrderLastDay = Game1.player.team.specialOrders.Any(so =>
      so != null && !so.IsHidden() && so.GetDaysLeft() == 1
    );

    return journalLastDay || specialOrderLastDay;
  }
  #endregion
}
