using System;
using System.Reflection;
using StardewModdingAPI;

namespace UIInfoSuite2Alt.Compatibility.Helpers;

internal static class FerngillEconomyHelper
{
  private static bool _isLoaded;
  private static PropertyInfo? _instanceProperty;
  private static PropertyInfo? _enableTooltipProperty;
  private static bool _reflectionInitialized;

  public static void Initialize(IModHelper helper)
  {
    _isLoaded = helper.ModRegistry.IsLoaded(ModCompat.FerngillEconomy);
    if (!_isLoaded)
      return;

    InitializeReflection();
  }

  /// <summary>
  /// Returns true if FSE's tooltip is enabled. Defaults to true if reflection failed (safe: keeps shift in place).
  /// </summary>
  public static bool IsTooltipEnabled()
  {
    if (!_isLoaded)
      return false;

    if (!_reflectionInitialized || _instanceProperty == null || _enableTooltipProperty == null)
      return true;

    try
    {
      object? instance = _instanceProperty.GetValue(null);
      return instance == null || (bool)(_enableTooltipProperty.GetValue(instance) ?? true);
    }
    catch (Exception ex)
    {
      ModEntry.MonitorObject.Log(
        $"FerngillEconomyHelper: failed to read EnableTooltip, {ex.Message}",
        LogLevel.Trace
      );
      return true;
    }
  }

  private static void InitializeReflection()
  {
    try
    {
      Type? configType = null;
      foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        try
        {
          configType = assembly.GetType("fse.core.models.ConfigModel");
          if (configType != null)
            break;
        }
        catch
        {
          // Skip assemblies that can't be inspected
        }
      }

      if (configType == null)
      {
        ModEntry.MonitorObject.Log(
          "FerngillEconomyHelper: could not find ConfigModel type via reflection",
          LogLevel.Warn
        );
        return;
      }

      _instanceProperty = configType.GetProperty(
        "Instance",
        BindingFlags.Static | BindingFlags.Public
      );
      if (_instanceProperty == null)
      {
        ModEntry.MonitorObject.Log(
          "FerngillEconomyHelper: could not find ConfigModel.Instance property",
          LogLevel.Warn
        );
        return;
      }

      _enableTooltipProperty = configType.GetProperty("EnableTooltip");
      _reflectionInitialized = _enableTooltipProperty != null;

      if (!_reflectionInitialized)
        ModEntry.MonitorObject.Log(
          "FerngillEconomyHelper: could not find EnableTooltip property on ConfigModel",
          LogLevel.Warn
        );
    }
    catch (Exception ex)
    {
      ModEntry.MonitorObject.Log(
        $"FerngillEconomyHelper: reflection setup failed, {ex.Message}",
        LogLevel.Warn
      );
    }
  }
}
