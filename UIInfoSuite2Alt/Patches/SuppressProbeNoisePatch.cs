using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using UIInfoSuite2Alt.Infrastructure.Helpers;

namespace UIInfoSuite2Alt.Patches;

/// <summary>
/// Silences Game1.showRedMessage while ArtisanPriceHelper is probing Data/Machines.
/// </summary>
internal static class SuppressProbeNoisePatch
{
  public static void Initialize(Harmony harmony)
  {
    harmony.Patch(
      original: AccessTools.Method(typeof(Game1), nameof(Game1.showRedMessage)),
      prefix: new HarmonyMethod(typeof(SuppressProbeNoisePatch), nameof(BeforeShowRedMessage))
    );

    ModEntry.MonitorObject.Log("SuppressProbeNoisePatch: initialized", LogLevel.Trace);
  }

  private static bool BeforeShowRedMessage()
  {
    return !ArtisanPriceHelper.IsProbing;
  }
}
