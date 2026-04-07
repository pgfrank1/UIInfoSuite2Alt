using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using UIInfoSuite2Alt.Compatibility;
using UIInfoSuite2Alt.Compatibility.Helpers;
using UIInfoSuite2Alt.Infrastructure;
using UIInfoSuite2Alt.Infrastructure.Helpers;
using UIInfoSuite2Alt.Options;
using UIInfoSuite2Alt.Patches;
using UIInfoSuite2Alt.UIElements;
using UIInfoSuite2Alt.UIElements.Menus;

namespace UIInfoSuite2Alt;

public partial class ModEntry : Mod
{
  public static ModConfig ModConfig { get; set; } = null!;

  private static EventHandler<ButtonsChangedEventArgs>? _calendarAndQuestKeyBindingsHandler;
  private static EventHandler<ButtonsChangedEventArgs>? _monsterEradicationKeyBindingsHandler;
  private static EventHandler<ButtonsChangedEventArgs>? _hideTreesKeyBindingsHandler;

  private static IModHelper _modHelper = null!;
  private ModOptionsPageHandler? _modOptionsPageHandler;
  private static Dictionary<string, string>? _lastConfigSnapshot;

  public static IReflectionHelper Reflection { get; private set; } = null!;

  internal const string CustomIconsAssetName = "Mods/DazUki.UIInfoSuite2Alt/CustomIcons";

  public static IMonitor MonitorObject { get; private set; } = null!;

  /// <summary>Save the global config.json to disk.</summary>
  public static void SaveConfig()
  {
    _modHelper.WriteConfig(ModConfig);

    var newSnapshot = ModConfig.SnapshotToggles();
    if (_lastConfigSnapshot != null)
    {
      List<string> changes = ModConfig.DiffToggles(_lastConfigSnapshot, newSnapshot);
      if (changes.Count > 0)
      {
        string diff = string.Join(", ", changes);
        MonitorObject.Log($"ModEntry: config saved, {diff}", LogLevel.Trace);
      }
    }

    _lastConfigSnapshot = newSnapshot;
  }

  #region Entry
  public override void Entry(IModHelper helper)
  {
    I18n.Init(helper.Translation);
    Reflection = helper.Reflection;
    MonitorObject = Monitor;
    _modHelper = helper;

    Monitor.Log($"Loaded v{ModManifest.Version}", LogLevel.Info);

    var harmony = new Harmony(ModManifest.UniqueID);
    TvChannelWatcher.Initialize(harmony, helper);
    ShowFishOnCatch.Initialize(harmony);
    HudMessagePatch.Initialize(harmony, helper.ModRegistry.IsLoaded(ModCompat.SpaceCore));
    ShowAccurateHearts.Initialize(harmony);
    ShowItemQualityPatch.Initialize(
      harmony,
      helper.ModRegistry.IsLoaded(ModCompat.ShowItemQuality)
    );
    HideTreesPatch.Initialize(harmony, helper);
    Monitor.Log(
      "ModEntry: Harmony patches applied - TvChannelWatcher, ShowFishOnCatch, HudMessagePatch, ShowAccurateHearts, ShowItemQualityPatch, HideTreesPatch",
      LogLevel.Trace
    );

    ModConfig = Helper.ReadConfig<ModConfig>();

    helper.Events.Content.AssetRequested += OnAssetRequested;
    helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
    helper.Events.GameLoop.DayStarted += OnDayStarted;
    helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    helper.Events.Display.RenderedHud += OnRenderedHud;

    RegisterCalendarAndQuestKeyBindings(helper, true);
    RegisterMonsterEradicationKeyBindings(helper, true);
    RegisterHideTreesKeyBinding(helper, true);

    IconHandler.Handler.IsQuestLogPermanent = helper.ModRegistry.IsLoaded(ModCompat.DeluxeJournal);

    if (helper.ModRegistry.IsLoaded(ModCompat.DailyTasksReportPlus))
    {
      IconHandler.Handler.ExtraYOffset = 18;
    }

    if (helper.ModRegistry.IsLoaded(ModCompat.BetterRanching))
    {
      Monitor.Log(
        "ModEntry: Better Ranching detected, disabling overlapping animal indicators",
        LogLevel.Info
      );
    }

    if (helper.ModRegistry.IsLoaded(ModCompat.BetterJunimos))
    {
      Monitor.Log(
        "ModEntry: Better Junimos detected, using configured Junimo hut radius",
        LogLevel.Info
      );
    }

    CheckForConflictingMods(helper);
  }
  #endregion

  #region Conflict detection
  private void CheckForConflictingMods(IModHelper helper)
  {
    var conflicts = new (string ModId, string Name)[]
    {
      (ModCompat.UIInfoSuite2, "UI Info Suite 2"),
      (ModCompat.UIInfoSuite, "UI Info Suite"),
    };

    foreach (var (modId, name) in conflicts)
    {
      if (helper.ModRegistry.IsLoaded(modId))
      {
        Monitor.Log(
          $"ModEntry: conflict detected - '{name}' ({modId}), both mods provide the same features, please remove one",
          LogLevel.Warn
        );
      }
    }
  }
  #endregion

  // GMCM registration is in Options/GmcmRegistration.cs (partial class)

  #region Mod recommendations
  private static void LogModRecommendations(IModHelper helper)
  {
    var recommendations = new (string ModId, string Name, int NexusId, string Reason)[]
    {
      (ModCompat.Gmcm, "Generic Mod Config Menu", 5098, "Required to Change Keybinds in-game"),
      (
        ModCompat.NpcMapLocations,
        "NPC Map Locations",
        239,
        "NPC map tracking was Removed in v2.7.0 - Use this mod instead"
      ),
    };

    foreach (var (modId, name, nexusId, reason) in recommendations)
    {
      if (!helper.ModRegistry.IsLoaded(modId))
      {
        MonitorObject.Log(
          $"ModEntry: recommended mod not installed - {name} [Nexus:{nexusId}], {reason}",
          LogLevel.Info
        );
      }
    }
  }
  #endregion

  #region Event subscriptions
  private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
  {
    // Only main player screen
    if (Context.ScreenId != 0)
    {
      return;
    }

    HideTreesPatch.Reset();
    _modOptionsPageHandler?.Dispose();
  }

  private void OnDayStarted(object? sender, DayStartedEventArgs e)
  {
    if (Context.ScreenId != 0)
    {
      return;
    }

    // Re-read config (may have been edited externally)
    ModConfig = Helper.ReadConfig<ModConfig>();
    BundleHelper.ClearCaches();
    UnlockableBundleHelper.ClearCache();
    ApplyFeatures();
  }

  /// <summary>Recreate feature handler to re-apply all toggles from current config.</summary>
  private void ApplyFeatures()
  {
    if (!Context.IsWorldReady)
    {
      return;
    }

    var currentSnapshot = ModConfig.SnapshotToggles();

    if (_lastConfigSnapshot == null)
    {
      string all = string.Join(", ", currentSnapshot.Select(kv => $"{kv.Key}={kv.Value}"));
      MonitorObject.Log($"ModEntry: initial config, {all}", LogLevel.Trace);
    }
    else
    {
      List<string> changes = ModConfig.DiffToggles(_lastConfigSnapshot, currentSnapshot);
      if (changes.Count == 1)
      {
        MonitorObject.Log($"ModEntry: GMCM config changed, {changes[0]}", LogLevel.Trace);
      }
      else if (changes.Count > 1)
      {
        string diff = string.Join("\n - ", changes);
        MonitorObject.Log($"ModEntry: GMCM config changed\n - {diff}", LogLevel.Trace);
      }
    }

    _lastConfigSnapshot = currentSnapshot;

    IconHandler.Handler.IconOrder = ModConfig.IconOrder;
    IconHandler.Handler.UseVerticalLayout = ModConfig.UseVerticalIconLayout;
    IconHandler.Handler.IconsPerRow = ModConfig.IconsPerRow;
    IconHandler.Handler.ShowQuestCount = ModConfig.ShowQuestCount;
    _modOptionsPageHandler?.Dispose();
    _modOptionsPageHandler = new ModOptionsPageHandler(Helper, ModConfig, SaveConfig);
  }

  private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
  {
    if (e.NameWithoutLocale.IsEquivalentTo(CustomIconsAssetName))
    {
      e.LoadFrom(() => new Dictionary<string, CustomIconData>(), AssetLoadPriority.Low);
    }
  }

  private static void OnRenderedHud(object? sender, RenderedHudEventArgs e)
  {
    IconHandler.Handler.DrawQueuedIcons(e.SpriteBatch);
  }

  public static void RegisterCalendarAndQuestKeyBindings(IModHelper helper, bool subscribe)
  {
    if (_calendarAndQuestKeyBindingsHandler == null)
    {
      _calendarAndQuestKeyBindingsHandler = (sender, e) =>
        HandleCalendarAndQuestKeyBindings(helper);
    }

    helper.Events.Input.ButtonsChanged -= _calendarAndQuestKeyBindingsHandler;

    if (subscribe)
    {
      helper.Events.Input.ButtonsChanged += _calendarAndQuestKeyBindingsHandler;
    }
  }

  private static void HandleCalendarAndQuestKeyBindings(IModHelper helper)
  {
    if (Context.IsPlayerFree)
    {
      if (ModConfig.OpenCalendarKeybind.JustPressed())
      {
        helper.Input.SuppressActiveKeybinds(ModConfig.OpenCalendarKeybind);
        Game1.activeClickableMenu = new Billboard();
      }
      else if (ModConfig.OpenQuestBoardKeybind.JustPressed())
      {
        helper.Input.SuppressActiveKeybinds(ModConfig.OpenQuestBoardKeybind);
        ShowCalendarAndBillboardOnGameMenuButton.OpenQuestBoardFromKeybind();
      }
      else if (ModConfig.OpenSpecialOrdersBoardKeybind.JustPressed())
      {
        helper.Input.SuppressActiveKeybinds(ModConfig.OpenSpecialOrdersBoardKeybind);
        ShowCalendarAndBillboardOnGameMenuButton.OpenSpecialOrdersBoardFromKeybind();
      }
    }
    else if (Game1.activeClickableMenu != null)
    {
      if (ModConfig.OpenCalendarKeybind.JustPressed() && Game1.activeClickableMenu is Billboard)
      {
        helper.Input.SuppressActiveKeybinds(ModConfig.OpenCalendarKeybind);
        Game1.playSound("bigDeSelect");
        Game1.exitActiveMenu();
      }
      else if (
        ModConfig.OpenQuestBoardKeybind.JustPressed()
        && Game1.activeClickableMenu is Billboard or QuestBoardSelector
      )
      {
        helper.Input.SuppressActiveKeybinds(ModConfig.OpenQuestBoardKeybind);
        Game1.playSound("bigDeSelect");
        Game1.exitActiveMenu();
      }
      else if (
        ModConfig.OpenSpecialOrdersBoardKeybind.JustPressed()
        && Game1.activeClickableMenu is SpecialOrdersBoard or SpecialOrdersBoardSelector
      )
      {
        helper.Input.SuppressActiveKeybinds(ModConfig.OpenSpecialOrdersBoardKeybind);
        Game1.playSound("bigDeSelect");
        Game1.exitActiveMenu();
      }
    }
  }

  public static void RegisterMonsterEradicationKeyBindings(IModHelper helper, bool subscribe)
  {
    if (_monsterEradicationKeyBindingsHandler == null)
    {
      _monsterEradicationKeyBindingsHandler = (sender, e) =>
        HandleMonsterEradicationKeyBindings(helper);
    }

    helper.Events.Input.ButtonsChanged -= _monsterEradicationKeyBindingsHandler;

    if (subscribe)
    {
      helper.Events.Input.ButtonsChanged += _monsterEradicationKeyBindingsHandler;
    }
  }

  private static void HandleMonsterEradicationKeyBindings(IModHelper helper)
  {
    if (Context.IsPlayerFree && ModConfig.OpenMonsterEradicationKeybind.JustPressed())
    {
      helper.Input.SuppressActiveKeybinds(ModConfig.OpenMonsterEradicationKeybind);
      MonsterQuestHelper.ShowMonsterKillList();
    }
  }

  public static void RegisterHideTreesKeyBinding(IModHelper helper, bool subscribe)
  {
    if (_hideTreesKeyBindingsHandler == null)
    {
      _hideTreesKeyBindingsHandler = (sender, e) => HandleHideTreesKeyBinding(helper);
    }

    helper.Events.Input.ButtonsChanged -= _hideTreesKeyBindingsHandler;

    if (subscribe)
    {
      helper.Events.Input.ButtonsChanged += _hideTreesKeyBindingsHandler;
    }
  }

  private static void HandleHideTreesKeyBinding(IModHelper helper)
  {
    if (Context.IsPlayerFree && ModConfig.HideTreesKeybind.JustPressed())
    {
      helper.Input.SuppressActiveKeybinds(ModConfig.HideTreesKeybind);
      HideTreesPatch.Toggle();
    }
  }
  #endregion
}
