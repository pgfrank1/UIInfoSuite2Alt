using System.Collections.Generic;
using StardewValley.GameData.Machines;

namespace UIInfoSuite2Alt.Compatibility;

/// <summary>The API provided by the Extra Machine Config mod (selph.ExtraMachineConfig).</summary>
public interface IExtraMachineConfigApi
{
  /// <summary>Retrieves the extra items consumed by this recipe beyond the primary input.</summary>
  IList<(string, int)> GetExtraRequirements(MachineItemOutput outputData);

  /// <summary>Retrieves the extra tag-defined items consumed by this recipe beyond the primary input.</summary>
  IList<(string, int)> GetExtraTagsRequirements(MachineItemOutput outputData);

  /// <summary>Retrieves the extra output items produced by this recipe beyond the primary output.</summary>
  IList<MachineItemOutput> GetExtraOutputs(MachineItemOutput outputData, MachineData? machine);
}
