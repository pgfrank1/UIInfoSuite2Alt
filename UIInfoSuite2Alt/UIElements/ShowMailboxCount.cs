using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;

namespace UIInfoSuite2Alt.UIElements;

internal class ShowMailboxCount : IDisposable
{
  private readonly IModHelper _helper;

  public ShowMailboxCount(IModHelper helper)
  {
    _helper = helper;
  }

  public void Dispose()
  {
    ToggleOption(false);
  }

  public void ToggleOption(bool enabled)
  {
    _helper.Events.Display.RenderedWorld -= OnRenderedWorld;

    if (enabled)
    {
      _helper.Events.Display.RenderedWorld += OnRenderedWorld;
    }
  }

  private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
  {
    int count = Game1.mailbox.Count;
    if (count <= 0)
    {
      return;
    }

    switch (Game1.currentLocation)
    {
      case Farm:
        {
          Point mailboxPosition = Game1.player.getMailboxPosition();
          DrawMailCount(e.SpriteBatch, count, mailboxPosition.X, mailboxPosition.Y, 0f);
          break;
        }
      case IslandWest island when island.farmhouseMailbox.Value:
        {
          // Island mailbox is at fixed tile (81, 40) with -8f x-offset matching vanilla
          DrawMailCount(e.SpriteBatch, count, 81, 40, -8f);
          break;
        }
    }
  }

  private static void DrawMailCount(SpriteBatch b, int count, int tileX, int tileY, float xOffset)
  {
    float bobbing =
      4f
      * (float)
        Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2);
    float layerDepth = (float)((tileX + 1) * 64) / 10000f + (float)(tileY * 64) / 10000f + 1E-04f;

    Vector2 globalPosition = new Vector2(tileX * 64 + 8 + xOffset, tileY * 64 - 96 - 48 + bobbing + 8);
    Vector2 localPosition = Game1.GlobalToLocal(Game1.viewport, globalPosition);

    float scale = 3f;
    Vector2 numberPos = localPosition + new Vector2(
      64f - Utility.getWidthOfTinyDigitString(count, scale) + scale,
      47f
    );

    Utility.drawTinyDigits(count, b, numberPos, scale, layerDepth, Color.White * 0.8f);
  }
}
