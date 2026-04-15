using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Machines;
using UIInfoSuite2Alt.Compatibility;
using Object = StardewValley.Object;

namespace UIInfoSuite2Alt.Infrastructure.Helpers;

/// <summary>
/// Probes Data/Machines to compute artisan good sell prices for a given input item.
/// Skips rules with C# delegate outputs and Extra Machine Config hidden secondary inputs
/// since their true profit isn't introspectable without subjective assumptions.
/// </summary>
public static class ArtisanPriceHelper
{
  public readonly struct ArtisanEntry
  {
    public readonly string MachineQualifiedId;
    public readonly int UnitSellPrice;
    public readonly int MinOutputStack;
    public readonly int MaxOutputStack;
    public readonly int InputsPerBatch;
    public readonly Item OutputItem;

    public ArtisanEntry(
      string machineQualifiedId,
      int unitSellPrice,
      int minOutputStack,
      int maxOutputStack,
      int inputsPerBatch,
      Item outputItem
    )
    {
      MachineQualifiedId = machineQualifiedId;
      UnitSellPrice = unitSellPrice;
      MinOutputStack = minOutputStack;
      MaxOutputStack = maxOutputStack;
      InputsPerBatch = inputsPerBatch;
      OutputItem = outputItem;
    }
  }

  public static readonly string[] MachineQualifiedIds =
  {
    "(BC)12", // Keg
    "(BC)15", // Preserves Jar
    "(BC)Dehydrator",
    "(BC)19", // Oil Maker
    "(BC)17", // Loom
    "(BC)16", // Cheese Press
    "(BC)24", // Mayonnaise Machine
    "(BC)FishSmoker",
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

      // Skip rules with a C# delegate output - we can't introspect it. Matches LookupAnything.
      if (outputData.OutputMethod != null)
      {
        continue;
      }

      // Skip rules with Extra Machine Config hidden secondary inputs - their true profit is
      // unknowable without subjective assumptions about fuel value.
      if (
        ApiManager.GetApi<IExtraMachineConfigApi>(ModCompat.ExtraMachineConfig, out var emcApi)
        && (
          emcApi.GetExtraRequirements(outputData).Count > 0
          || emcApi.GetExtraTagsRequirements(outputData).Count > 0
        )
      )
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

      int minStack = outputData.MinStack > 0 ? outputData.MinStack : 1;
      int maxStack = outputData.MaxStack > 0 ? outputData.MaxStack : minStack;
      if (maxStack < minStack)
      {
        maxStack = minStack;
      }

      int inputsPerBatch = triggerRule?.RequiredCount > 0 ? triggerRule.RequiredCount : 1;
      result[i] = new ArtisanEntry(machineQid, unit, minStack, maxStack, inputsPerBatch, outObj);
    }

    _cache[cacheKey] = result;
    return result;
  }
}
