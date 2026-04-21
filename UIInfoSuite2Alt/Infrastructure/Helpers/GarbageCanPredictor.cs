using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.GarbageCans;
using UIInfoSuite2Alt.Compatibility;
using UIInfoSuite2Alt.Compatibility.Helpers;

namespace UIInfoSuite2Alt.Infrastructure.Helpers;

/// <summary>
/// Predicts today's garbage can drops. Reads GarbageDay chests directly when present,
/// otherwise delegates to <see cref="GameLocation.TryGetGarbageItem"/> for vanilla/modded
/// cans defined in <c>Data/GarbageCans</c>, and respects BinningSkill level gating.
/// </summary>
internal static class GarbageCanPredictor
{
  private const string BinningMinLevelField = "drbirbdev.BinningSkill_MinLevel";
  private const string BinningSkillId = "drbirbdev.Binning";

  public static void Predict(
    GameLocation location,
    string id,
    Vector2 tile,
    Farmer farmer,
    out List<Item> items,
    out bool alreadyChecked,
    out int? lockedMinLevel,
    out bool fromGarbageDayChest
  )
  {
    items = [];
    alreadyChecked = false;
    lockedMinLevel = null;
    fromGarbageDayChest = false;

    // GarbageDay: read actual pre-rolled items from the chest (may accumulate across days)
    if (GarbageDayHelper.TryGetGarbageCan(location, tile, out _, out List<Item> chestItems))
    {
      items = chestItems;
      fromGarbageDayChest = true;
      return;
    }

    alreadyChecked = Game1.netWorldState.Value.CheckedGarbage.Contains(id);

    if (TryGetBinningLockLevel(id, farmer, out int required))
    {
      lockedMinLevel = required;
      return;
    }

    if (
      location.TryGetGarbageItem(
        id,
        farmer.DailyLuck,
        out Item? item,
        out _,
        out _,
        LogGarbageError
      )
      && item != null
    )
    {
      items.Add(item);
    }
  }

  private static void LogGarbageError(string message)
  {
    ModEntry.MonitorObject.Log($"GarbageCanPredictor: {message}", LogLevel.Trace);
  }

  private static bool TryGetBinningLockLevel(string id, Farmer farmer, out int requiredLevel)
  {
    requiredLevel = 0;

    if (!ApiManager.GetApi(ModCompat.SpaceCore, out ISpaceCoreApi? spaceCore))
    {
      return false;
    }

    GarbageCanData data = DataLoader.GarbageCans(Game1.content);
    if (
      !data.GarbageCans.TryGetValue(id, out GarbageCanEntryData? entry)
      || entry.CustomFields == null
      || !entry.CustomFields.TryGetValue(BinningMinLevelField, out string? minLevelStr)
      || !int.TryParse(minLevelStr, out int minLevel)
      || minLevel <= 0
    )
    {
      return false;
    }

    int currentLevel = spaceCore.GetLevelForCustomSkill(farmer, BinningSkillId);
    if (currentLevel >= minLevel)
    {
      return false;
    }

    requiredLevel = minLevel;
    return true;
  }
}
