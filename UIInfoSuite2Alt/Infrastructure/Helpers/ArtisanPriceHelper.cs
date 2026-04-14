using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Machines;
using Object = StardewValley.Object;

namespace UIInfoSuite2Alt.Infrastructure.Helpers;

/// <summary>
/// Probes Data/Machines to compute artisan good sell prices for a given input item.
/// Currently covers Keg, Preserves Jar, and Dehydrator.
/// </summary>
public static class ArtisanPriceHelper
{
  public readonly struct ArtisanEntry
  {
    public readonly string MachineQualifiedId;
    public readonly int UnitSellPrice;
    public readonly int OutputStackPerInput;
    public readonly int InputsPerBatch;
    public readonly Item OutputItem;

    public ArtisanEntry(
      string machineQualifiedId,
      int unitSellPrice,
      int outputStackPerInput,
      int inputsPerBatch,
      Item outputItem
    )
    {
      MachineQualifiedId = machineQualifiedId;
      UnitSellPrice = unitSellPrice;
      OutputStackPerInput = outputStackPerInput;
      InputsPerBatch = inputsPerBatch;
      OutputItem = outputItem;
    }
  }

  public static readonly string[] MachineQualifiedIds =
  {
    "(BC)12", // Keg
    "(BC)15", // Preserves Jar
    "(BC)Dehydrator",
  };

  private static readonly Dictionary<string, ArtisanEntry?[]> _cache = new();
  private static bool _eventsHooked;

  public static void EnsureInitialized(IModHelper helper)
  {
    if (_eventsHooked)
    {
      return;
    }

    helper.Events.GameLoop.DayStarted += (_, _) => _cache.Clear();
    helper.Events.Content.AssetReady += OnAssetReady;
    _eventsHooked = true;
  }

  private static void OnAssetReady(object? sender, AssetReadyEventArgs e)
  {
    if (e.Name.IsEquivalentTo("Data/Machines") || e.Name.IsEquivalentTo("Data/Objects"))
    {
      _cache.Clear();
    }
  }

  /// <summary>Returns artisan entries parallel to <see cref="MachineQualifiedIds"/>. Null = not accepted.</summary>
  public static ArtisanEntry?[] GetEntries(Item? input)
  {
    if (input is not Object inputObj || inputObj.QualifiedItemId == null)
    {
      return new ArtisanEntry?[MachineQualifiedIds.Length];
    }

    string cacheKey = inputObj.QualifiedItemId + "|" + inputObj.Quality;
    if (_cache.TryGetValue(cacheKey, out ArtisanEntry?[]? cached))
    {
      return cached;
    }

    var result = new ArtisanEntry?[MachineQualifiedIds.Length];
    Dictionary<string, MachineData> machinesData = DataLoader.Machines(Game1.content);
    GameLocation location = Game1.currentLocation ?? Game1.getFarm();

    for (int i = 0; i < MachineQualifiedIds.Length; i++)
    {
      string machineQid = MachineQualifiedIds[i];
      if (
        !machinesData.TryGetValue(machineQid, out MachineData? machineData)
        || machineData == null
      )
      {
        continue;
      }

      Object machine;
      try
      {
        machine = (Object)ItemRegistry.Create(machineQid);
      }
      catch
      {
        continue;
      }

      // Probe input (fresh copy to avoid any mutation bleed). Stack is inflated so that
      // machine rules with RequiredCount > 1 (e.g. Dehydrator needs 5, Keg coffee needs 5)
      // still match during probing.
      Item probeInput = inputObj.getOne();
      probeInput.Quality = inputObj.Quality;
      probeInput.Stack = 999;

      if (
        !MachineDataUtility.TryGetMachineOutputRule(
          machine,
          machineData,
          MachineOutputTrigger.ItemPlacedInMachine,
          probeInput,
          Game1.player,
          location,
          out MachineOutputRule? rule,
          out MachineOutputTriggerRule? triggerRule,
          out _,
          out _
        )
      )
      {
        continue;
      }

      MachineItemOutput? outputData = MachineDataUtility.GetOutputData(
        machine,
        machineData,
        rule,
        probeInput,
        Game1.player,
        location
      );
      if (outputData == null)
      {
        continue;
      }

      Item? output = MachineDataUtility.GetOutputItem(
        machine,
        outputData,
        probeInput,
        Game1.player,
        probe: true,
        out _
      );

      if (output is not Object outObj || outObj.Price <= 0)
      {
        continue;
      }

      int unit = Tools.GetSellToStorePrice(outObj);
      if (unit <= 0)
      {
        continue;
      }

      int outputStack = outObj.Stack > 0 ? outObj.Stack : 1;
      int inputsPerBatch = triggerRule?.RequiredCount > 0 ? triggerRule.RequiredCount : 1;
      result[i] = new ArtisanEntry(machineQid, unit, outputStack, inputsPerBatch, outObj);
    }

    _cache[cacheKey] = result;
    return result;
  }
}
