using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace UIInfoSuite2Alt.UIElements.DonationIcons;

internal interface IDonationIconProvider
{
  /// <summary>Whether the provider is available (mod loaded, location valid, etc.).</summary>
  bool IsAvailable { get; }

  /// <summary>Whether this item needs donating to this provider's collection.</summary>
  bool NeedsDonation(Item item);

  /// <summary>The icon texture and source rectangle. Null if the icon failed to load.</summary>
  (Texture2D texture, Rectangle sourceRect)? GetIcon();
}
