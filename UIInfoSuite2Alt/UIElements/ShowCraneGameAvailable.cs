using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using UIInfoSuite2Alt.Infrastructure;

namespace UIInfoSuite2Alt.UIElements;

internal class ShowCraneGameAvailable : IDisposable
{
  private static readonly Rectangle CraneSourceRect = new(40, 214, 20, 20);

  private readonly PerScreen<bool> _isAvailable = new();
  private readonly PerScreen<Rectangle> _iconBounds = new(() => new Rectangle(0, 0, 40, 40));

  private readonly Texture2D _borderTexture;
  private readonly IModHelper _helper;

  public ShowCraneGameAvailable(IModHelper helper)
  {
    _helper = helper;
    Texture2D source = AssetHelper.TryLoadTextureFromFile(
      Path.Combine(helper.DirectoryPath, "assets", "weatherbox.png")
    );
    if (AssetHelper.IsFallback(source))
    {
      _borderTexture = source;
    }
    else
    {
      _borderTexture = Tools.RecolorTexture(source, Color.SteelBlue * 0.5f);
      source.Dispose();
    }
  }

  public void Dispose()
  {
    ToggleOption(false);
    if (!AssetHelper.IsFallback(_borderTexture))
      _borderTexture.Dispose();
  }

  public void ToggleOption(bool enabled)
  {
    _helper.Events.Display.RenderingHud -= OnRenderingHud;
    _helper.Events.GameLoop.DayStarted -= OnDayStarted;
    _helper.Events.GameLoop.TimeChanged -= OnTimeChanged;

    if (enabled)
    {
      CheckAvailability();
      _helper.Events.Display.RenderingHud += OnRenderingHud;
      _helper.Events.GameLoop.DayStarted += OnDayStarted;
      _helper.Events.GameLoop.TimeChanged += OnTimeChanged;
    }
  }

  private void CheckAvailability()
  {
    _isAvailable.Value = false;

    if (
      !Context.IsWorldReady
      || !Game1.MasterPlayer.hasOrWillReceiveMail("ccMovieTheater")
      || Game1.player.lastSeenMovieWeek.Value >= Game1.Date.TotalWeeks
      || Game1.getLocationFromName("MovieTheater") is not MovieTheater theater
      || theater.dayFirstEntered.Value == -1
      || theater.dayFirstEntered.Value == Game1.Date.TotalDays
    )
      return;

    // 25% chance the crane man is present (occupied)
    if (Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.Date.TotalDays).NextDouble() < 0.25)
      return;

    _isAvailable.Value = true;
  }

  private void OnDayStarted(object? sender, DayStartedEventArgs e)
  {
    CheckAvailability();
  }

  private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
  {
    if (!_isAvailable.Value)
      return;

    // Hide once the movie has started or ended
    if (!(Game1.getLocationFromName("MovieTheater") is MovieTheater theater))
      return;

    int state = _helper.Reflection.GetField<NetInt>(theater, "currentState").GetValue()?.Value ?? 0;
    if (state > 0)
      _isAvailable.Value = false;
  }

  private void OnRenderingHud(object? sender, RenderingHudEventArgs e)
  {
    if (!UIElementUtils.IsRenderingNormally() || !_isAvailable.Value)
      return;

    Rectangle bounds = _iconBounds.Value;

    IconHandler.Handler.EnqueueIcon(
      "Festival",
      (batch, pos) =>
      {
        bounds.X = pos.X;
        bounds.Y = pos.Y;
        _iconBounds.Value = bounds;

        batch.Draw(_borderTexture, bounds, Color.White);
        batch.Draw(
          _helper.GameContent.Load<Texture2D>("Maps/MovieTheater_TileSheet"),
          new Rectangle(pos.X + 3, pos.Y + 3, 34, 34),
          CraneSourceRect,
          Color.White
        );
      },
      batch =>
      {
        if (bounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
          IClickableMenu.drawHoverText(batch, I18n.CraneGameAvailable(), Game1.smallFont);
      }
    );
  }
}
