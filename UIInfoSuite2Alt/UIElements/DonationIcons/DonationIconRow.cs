using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace UIInfoSuite2Alt.UIElements.DonationIcons;

/// <summary>
/// Collects donation icon providers and draws applicable icons in a horizontal row.
/// The first icon draws at the anchor position; additional icons fill to the left.
/// </summary>
internal class DonationIconRow
{
  private const float IconScale = 2f;
  private const float LayerDepth = 0.86f;
  private const int IconSpacing = 4;

  private readonly List<IDonationIconProvider> _providers = new();

  public void AddProvider(IDonationIconProvider provider)
  {
    _providers.Add(provider);
  }

  /// <summary>
  /// Checks whether any provider needs a donation for this item.
  /// </summary>
  public bool HasAnyDonation(Item item)
  {
    for (int i = 0; i < _providers.Count; i++)
    {
      IDonationIconProvider provider = _providers[i];
      if (provider.IsAvailable && provider.NeedsDonation(item))
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>
  /// Draws all applicable donation icons. First icon at anchor, rest to the left.
  /// </summary>
  /// <returns>True if any icons were drawn.</returns>
  public bool Draw(SpriteBatch spriteBatch, Item item, Vector2 anchorPos)
  {
    // Collect applicable icons
    var icons = new List<(Texture2D texture, Rectangle sourceRect)>();
    for (int i = 0; i < _providers.Count; i++)
    {
      IDonationIconProvider provider = _providers[i];
      if (provider.IsAvailable && provider.NeedsDonation(item))
      {
        (Texture2D texture, Rectangle sourceRect)? icon = provider.GetIcon();
        if (icon.HasValue)
        {
          icons.Add(icon.Value);
        }
      }
    }

    if (icons.Count == 0)
    {
      return false;
    }

    // Draw first icon at anchor, rest to the left
    float xOffset = 0;
    for (int i = 0; i < icons.Count; i++)
    {
      (Texture2D texture, Rectangle sourceRect) = icons[i];

      // Origin at bottom-center of sprite (matching original museum icon draw)
      var origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height);
      float scaledWidth = sourceRect.Width * IconScale;

      Vector2 pos = anchorPos + new Vector2(-xOffset, 0);

      spriteBatch.Draw(
        texture,
        pos,
        sourceRect,
        Color.White,
        0f,
        origin,
        IconScale,
        SpriteEffects.None,
        LayerDepth
      );

      xOffset += scaledWidth + IconSpacing;
    }

    return true;
  }
}
