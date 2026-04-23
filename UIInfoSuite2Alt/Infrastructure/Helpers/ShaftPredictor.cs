using StardewValley;

namespace UIInfoSuite2Alt.Infrastructure.Helpers;

/// <summary>
/// Predicts how many floors a Skull Cavern shaft ("jump-in hole") will drop the player.
/// Replicates the vanilla <c>MineShaft.enterMineShaft</c> formula; deterministic per
/// (mineLevel, save seed, in-game date), identical for every shaft on the same floor/day.
/// </summary>
internal static class ShaftPredictor
{
  /// <summary>Maximum floor reachable via shaft fall.</summary>
  private const int MaxFloor = 220;

  /// <summary>
  /// Computes the number of floors the player will fall when jumping into a shaft on
  /// <paramref name="mineLevel"/>.
  /// </summary>
  public static int PredictFallDistance(int mineLevel)
  {
    System.Random random = Utility.CreateRandom(
      mineLevel,
      Game1.uniqueIDForThisGame,
      Game1.Date.TotalDays
    );
    int fall = random.Next(3, 9);
    if (random.NextDouble() < 0.1)
    {
      fall = fall * 2 - 1;
    }

    if (mineLevel < MaxFloor && mineLevel + fall > MaxFloor)
    {
      fall = MaxFloor - mineLevel;
    }

    return fall;
  }
}
