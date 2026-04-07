using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using UIInfoSuite2Alt.Infrastructure.Extensions;

namespace UIInfoSuite2Alt.UIElements.DonationIcons;

internal class MuseumDonationProvider : IDonationIconProvider
{
  private LibraryMuseum? _libraryMuseum;
  private (Texture2D texture, Rectangle sourceRect)? _icon;
  private bool _iconInitialized;

  public bool IsAvailable => _libraryMuseum != null;

  public void Initialize()
  {
    _libraryMuseum = Game1.getLocationFromName("ArchaeologyHouse") as LibraryMuseum;
    _iconInitialized = false;
  }

  public bool NeedsDonation(Item item)
  {
    return _libraryMuseum?.isItemSuitableForDonation(item) ?? false;
  }

  public (Texture2D texture, Rectangle sourceRect)? GetIcon()
  {
    if (_iconInitialized)
    {
      return _icon;
    }

    _iconInitialized = true;

    NPC? gunther = Game1.getCharacterFromName("Gunther");
    if (gunther == null)
    {
      ModEntry.MonitorObject.Log(
        "MuseumDonationProvider: Gunther not found, creating fallback NPC",
        LogLevel.Warn
      );
      gunther = new NPC
      {
        Name = "Gunther",
        Age = 0,
        Sprite = new AnimatedSprite("Characters\\Gunther"),
      };
    }

    _icon = (gunther.Sprite.Texture, gunther.GetHeadShot());
    return _icon;
  }
}
