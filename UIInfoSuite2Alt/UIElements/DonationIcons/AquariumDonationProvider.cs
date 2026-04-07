using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using UIInfoSuite2Alt.Compatibility;
using UIInfoSuite2Alt.Compatibility.Helpers;

namespace UIInfoSuite2Alt.UIElements.DonationIcons;

internal class AquariumDonationProvider : IDonationIconProvider
{
  private readonly IModHelper _helper;
  private (Texture2D texture, Rectangle sourceRect)? _icon;
  private bool _iconInitialized;

  public AquariumDonationProvider(IModHelper helper)
  {
    _helper = helper;
    AquariumHelper.Initialize(helper);
  }

  public bool IsAvailable => AquariumHelper.IsModLoaded;

  public bool NeedsDonation(Item item)
  {
    return AquariumHelper.IsUndonatedAquariumFish(item);
  }

  public (Texture2D texture, Rectangle sourceRect)? GetIcon()
  {
    if (_iconInitialized)
    {
      return _icon;
    }

    _iconInitialized = true;
    if (!AquariumHelper.IsModLoaded)
    {
      return null;
    }

    try
    {
      Texture2D curatorTexture = _helper.GameContent.Load<Texture2D>("Characters/Curator");
      _icon = (curatorTexture, new Rectangle(0, 1, 16, 16));
    }
    catch (Exception)
    {
      ModEntry.MonitorObject.Log(
        "AquariumDonationProvider: Stardew Aquarium installed but Curator sprite load failed",
        LogLevel.Warn
      );
    }

    return _icon;
  }
}
