using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace UIInfoSuite2Alt.Compatibility.Helpers;

/// <summary>
/// Compat for LeFauxMatt's Garbage Day mod which replaces vanilla garbage cans with chests.
/// Lets us read the actual pre-rolled items from the chest instead of predicting.
/// </summary>
public static class GarbageDayHelper
{
  private const string ChestInventoryPrefix = "furyx639.GarbageDay-";

  public static bool TryGetGarbageCan(
    GameLocation location,
    Vector2 tile,
    out string id,
    out List<Item> items
  )
  {
    id = string.Empty;
    items = [];

    if (
      !location.Objects.TryGetValue(tile, out StardewValley.Object? obj) || obj is not Chest chest
    )
    {
      return false;
    }

    string? globalInventoryId = chest.GlobalInventoryId;
    if (
      globalInventoryId == null
      || !globalInventoryId.StartsWith(ChestInventoryPrefix, StringComparison.OrdinalIgnoreCase)
    )
    {
      return false;
    }

    id = globalInventoryId.Substring(ChestInventoryPrefix.Length);

    foreach (Item item in chest.GetItemsForPlayer())
    {
      if (item != null)
      {
        items.Add(item);
      }
    }

    return true;
  }
}
