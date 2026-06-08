using System;
using StardewValley;
using StardewValley.TerrainFeatures;
using UIInfoSuite2Alt.Compatibility;

namespace UIInfoSuite2Alt.Infrastructure.Helpers;

/// <summary>
/// Predicts harvest quality by replicating the game's seeded random rolls.
/// Quality 0 = normal, 1 = silver, 2 = gold, 4 = iridium.
/// </summary>
internal static class QualityPrediction
{
  public static int PredictForageableQuality(float tileX, float tileY, Farmer farmer)
  {
    if (farmer.professions.Contains(Farmer.botanist))
    {
      return GetBotanistForageQuality(farmer);
    }

    var random = Utility.CreateDaySaveRandom(tileX, tileY * 777f);
    return RollForageQuality(random, farmer.ForagingLevel);
  }

  public static int PredictForageCropQuality(
    int tileX,
    int tileY,
    string whichForageCrop,
    Farmer farmer
  )
  {
    // Ginger (forageCrop "2") has no quality rolls
    if (whichForageCrop == "2")
    {
      return 0;
    }

    if (farmer.professions.Contains(Farmer.botanist))
    {
      return GetBotanistForageQuality(farmer);
    }

    var random = Utility.CreateDaySaveRandom(tileX * 1000, tileY * 2000);
    return RollForageQuality(random, farmer.ForagingLevel);
  }

  public static int PredictCropQuality(int tileX, int tileY, HoeDirt soil, Crop crop)
  {
    var random = Utility.CreateRandom(
      tileX * 7.0,
      tileY * 11.0,
      Game1.stats.DaysPlayed,
      Game1.uniqueIDForThisGame
    );

    int fertilizerLevel = soil.GetFertilizerQualityBoostLevel();
    double baseChance =
      0.2 * (Game1.player.FarmingLevel / 10.0)
      + 0.2 * fertilizerLevel * ((Game1.player.FarmingLevel + 2.0) / 12.0)
      + 0.01;
    double silverChance = Math.Min(0.75, baseChance * 2.0);

    int quality = 0;
    if (fertilizerLevel >= 3 && random.NextDouble() < baseChance / 2.0)
    {
      quality = 4;
    }
    else if (random.NextDouble() < baseChance)
    {
      quality = 2;
    }
    else if (random.NextDouble() < silverChance || fertilizerLevel >= 3)
    {
      quality = 1;
    }

    // Respect modded crop data quality constraints
    var data = crop.GetData();
    if (data != null)
    {
      int min = data.HarvestMinQuality;
      int max = Math.Max(min, data.HarvestMaxQuality ?? quality);
      quality = Math.Clamp(quality, min, max);
    }

    return quality;
  }

  public static int PredictCropOnTile(HoeDirt soil, int tileX, int tileY)
  {
    Crop? crop = soil.crop;
    if (crop == null || crop.dead.Value)
    {
      return -1;
    }

    // Crop not ready for harvest
    if (crop.currentPhase.Value < crop.phaseDays.Count - 1)
    {
      return -1;
    }

    // Regrowable crop that isn't ready yet
    if (crop.fullyGrown.Value && crop.dayOfCurrentPhase.Value > 0)
    {
      return -1;
    }

    if (crop.forageCrop.Value)
    {
      return PredictForageCropQuality(tileX, tileY, crop.whichForageCrop.Value, Game1.player);
    }

    return PredictCropQuality(tileX, tileY, soil, crop);
  }

  /// <summary>WoL Ecologist quality if available, vanilla iridium otherwise.</summary>
  private static int GetBotanistForageQuality(Farmer farmer)
  {
    if (ApiManager.GetApi<IWalkOfLifeApi>(ModCompat.WalkOfLife, out var wolApi))
    {
      return wolApi.GetEcologistForageQuality(farmer);
    }

    return 4;
  }

  /// <summary>WoL Gemologist mineral quality, or -1 if unavailable.</summary>
  public static int GetGemologistMineralQuality(Farmer farmer)
  {
    if (
      ApiManager.GetApi<IWalkOfLifeApi>(ModCompat.WalkOfLife, out var wolApi)
      && farmer.professions.Contains(Farmer.gemologist)
    )
    {
      return wolApi.GetGemologistMineralQuality(farmer);
    }

    return -1;
  }

  private static int RollForageQuality(Random random, int foragingLevel)
  {
    if (random.NextDouble() < foragingLevel / 30f)
    {
      return 2;
    }

    if (random.NextDouble() < foragingLevel / 15f)
    {
      return 1;
    }

    return 0;
  }
}
