using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace UIInfoSuite2Alt.Compatibility.Helpers;

public class CachedCustomSkillInfo(
  Texture2D icon,
  Color barColor,
  int[] experienceCurve,
  string displayName
)
{
  public Texture2D Icon { get; } = icon;
  public Color BarColor { get; } = barColor;
  public int[] ExperienceCurve { get; } = experienceCurve;
  public string DisplayName { get; } = displayName;
}

public static class SpaceCoreHelper
{
  private static readonly Color DefaultBarColor = new Color(148, 103, 198) * 0.63f;

  private static readonly Dictionary<string, CachedCustomSkillInfo> SkillCache = [];
  private static MethodInfo? _getSkillMethod;
  private static bool _reflectionAttempted;

  public static CachedCustomSkillInfo GetSkillInfo(ISpaceCoreApi api, string skillId)
  {
    if (SkillCache.TryGetValue(skillId, out CachedCustomSkillInfo? cached))
    {
      return cached;
    }

    Texture2D icon = api.GetSkillPageIconForCustomSkill(skillId);
    string displayName = api.GetDisplayNameOfCustomSkill(skillId);

    Color barColor = DefaultBarColor;
    int[] experienceCurve = [];

    // Reflection to get ExperienceBarColor and ExperienceCurve from internal Skill object
    object? skillObject = GetSkillObject(skillId);
    if (skillObject != null)
    {
      try
      {
        Type skillType = skillObject.GetType();

        PropertyInfo? colorProp = skillType.GetProperty("ExperienceBarColor");
        if (colorProp?.GetValue(skillObject) is Color color)
        {
          barColor = color;
        }

        PropertyInfo? curveProp = skillType.GetProperty("ExperienceCurve");
        if (curveProp?.GetValue(skillObject) is int[] curve)
        {
          experienceCurve = curve;
        }
      }
      catch (Exception ex)
      {
        ModEntry.MonitorObject.Log(
          $"SpaceCoreHelper: reflection failed for skill '{skillId}', {ex.Message}",
          LogLevel.Warn
        );
      }
    }

    var info = new CachedCustomSkillInfo(icon, barColor, experienceCurve, displayName);

    SkillCache[skillId] = info;
    ModEntry.MonitorObject.Log(
      $"SpaceCoreHelper: skill cached, id={skillId}, name={displayName}, barColor={barColor}, levels={experienceCurve.Length}",
      LogLevel.Trace
    );
    return info;
  }

  public static int GetExperienceRequiredForLevel(CachedCustomSkillInfo info, int level)
  {
    if (level < 0)
    {
      return 0;
    }

    if (level < info.ExperienceCurve.Length)
    {
      return info.ExperienceCurve[level];
    }

    // Level beyond curve = maxed
    return -1;
  }

  public static void ClearCache()
  {
    SkillCache.Clear();
  }

  private static object? GetSkillObject(string skillId)
  {
    if (!_reflectionAttempted)
    {
      _reflectionAttempted = true;
      try
      {
        Type? skillsType = AccessTools.TypeByName("SpaceCore.Skills");
        _getSkillMethod = skillsType?.GetMethod(
          "GetSkill",
          BindingFlags.Public | BindingFlags.Static,
          null,
          [typeof(string)],
          null
        );
        ModEntry.MonitorObject.Log(
          $"SpaceCoreHelper: reflection resolved, skillsType={skillsType != null}, getSkillMethod={_getSkillMethod != null}",
          LogLevel.Trace
        );
      }
      catch (Exception ex)
      {
        ModEntry.MonitorObject.Log(
          $"SpaceCoreHelper: reflection failed, {ex.Message}",
          LogLevel.Warn
        );
      }
    }

    if (_getSkillMethod == null)
    {
      return null;
    }

    try
    {
      return _getSkillMethod.Invoke(null, [skillId]);
    }
    catch (Exception ex)
    {
      ModEntry.MonitorObject.Log(
        $"SpaceCoreHelper: GetSkill invocation failed for '{skillId}', {ex.Message}",
        LogLevel.Trace
      );
      return null;
    }
  }
}
