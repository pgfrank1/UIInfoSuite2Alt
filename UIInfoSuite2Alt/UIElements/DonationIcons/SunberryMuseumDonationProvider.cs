using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using UIInfoSuite2Alt.Compatibility;
using UIInfoSuite2Alt.Infrastructure.Extensions;

namespace UIInfoSuite2Alt.UIElements.DonationIcons;

internal class SunberryMuseumDonationProvider : IDonationIconProvider
{
  private const string DropBoxId = "SBVMuseumBox";
  private const string OwnerNpcName = "EliasSBV";
  private static readonly Rectangle LetterFallbackRect = new(189, 423, 16, 16);

  private readonly IModHelper _helper;
  private readonly bool _isModLoaded;
  private (Texture2D texture, Rectangle sourceRect)? _icon;
  private bool _iconInitialized;

  public SunberryMuseumDonationProvider(IModHelper helper)
  {
    _helper = helper;
    _isModLoaded = helper.ModRegistry.IsLoaded(ModCompat.SunberryVillageCp);
  }

  public bool IsAvailable => _isModLoaded;

  public bool NeedsDonation(Item item)
  {
    if (Game1.player?.team?.specialOrders is null)
    {
      return false;
    }

    foreach (SpecialOrder order in Game1.player.team.specialOrders)
    {
      if (order?.objectives is null)
      {
        continue;
      }

      foreach (OrderObjective objective in order.objectives)
      {
        if (objective is not DonateObjective donate)
        {
          continue;
        }

        if (donate.dropBox.Value != DropBoxId)
        {
          continue;
        }

        if (donate.GetCount() >= donate.GetMaxCount())
        {
          continue;
        }

        if (donate.IsValidItem(item))
        {
          return true;
        }
      }
    }

    return false;
  }

  public (Texture2D texture, Rectangle sourceRect)? GetIcon()
  {
    if (_iconInitialized)
    {
      return _icon;
    }

    (Texture2D texture, Rectangle sourceRect)? resolved =
      ResolveOwnerHeadshot() ?? ResolveFallback();
    if (resolved is null)
    {
      return null;
    }

    _iconInitialized = true;
    _icon = resolved;
    return _icon;
  }

  private static (Texture2D texture, Rectangle sourceRect)? ResolveOwnerHeadshot()
  {
    NPC? owner = Game1.getCharacterFromName(OwnerNpcName);
    if (owner?.Sprite?.Texture is null)
    {
      return null;
    }

    return (owner.Sprite.Texture, owner.GetHeadShot());
  }

  private (Texture2D texture, Rectangle sourceRect)? ResolveFallback()
  {
    try
    {
      Texture2D cursors = _helper.GameContent.Load<Texture2D>("LooseSprites/Cursors");
      return (cursors, LetterFallbackRect);
    }
    catch (Exception ex)
    {
      ModEntry.MonitorObject.Log(
        $"SunberryMuseumDonationProvider: fallback icon load failed, message={ex.Message}",
        LogLevel.Warn
      );
      return null;
    }
  }
}
