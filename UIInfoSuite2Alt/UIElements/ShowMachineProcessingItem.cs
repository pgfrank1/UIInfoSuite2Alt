using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.ItemTypeDefinitions;
using Object = StardewValley.Object;

namespace UIInfoSuite2Alt.UIElements;

internal class ShowMachineProcessingItem : IDisposable
{
  // Custom icon offsets from the machine's sprite center
  private static readonly Dictionary<string, Vector2> CustomOffsets = new()
  {
    ["Cask"] = new Vector2(0f, -20f),
  };

  private const string BeeHouseQualifiedId = "(BC)10";

  private readonly PerScreen<List<MachineIconData>> _visibleMachines = new(() => []);
  private readonly PerScreen<List<FishPondIconData>> _visibleFishPonds = new(() => []);

  private readonly IModHelper _helper;
  private bool _showMachineIcons;
  private bool _showFishPondIcons;
  private readonly PerScreen<bool> _toggleState = new(() =>
    ModEntry.ModConfig.MachineProcessingIconsVisible
  );

  public ShowMachineProcessingItem(IModHelper helper)
  {
    _helper = helper;
    _helper.Events.Input.ButtonsChanged += OnButtonsChanged;
    _helper.Events.GameLoop.Saving += OnSaving;
  }

  public void Dispose()
  {
    ToggleOption(false);
    ToggleFishPondOption(false);
    _helper.Events.Input.ButtonsChanged -= OnButtonsChanged;
    _helper.Events.GameLoop.Saving -= OnSaving;
  }

  public void SetMode(int mode)
  {
    _showMachineIcons = mode switch
    {
      1 => _toggleState.Value,
      2 => true,
      _ => false,
    };
    UpdateEventSubscriptions();
  }

  public void ToggleOption(bool enabled)
  {
    _showMachineIcons = enabled;
    UpdateEventSubscriptions();
  }

  public void ToggleFishPondOption(bool enabled)
  {
    _showFishPondIcons = enabled;
    UpdateEventSubscriptions();
  }

  private void UpdateEventSubscriptions()
  {
    _helper.Events.Display.RenderedWorld -= OnRenderedWorld;
    _helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;

    if (_showMachineIcons || _showFishPondIcons)
    {
      _helper.Events.Display.RenderedWorld += OnRenderedWorld;
      _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }
  }

  private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
  {
    KeybindList keybind = ModEntry.ModConfig.ToggleMachineProcessingIcons;
    if (!Context.IsPlayerFree || !keybind.JustPressed())
    {
      return;
    }

    // Keybind only works in Toggle mode (temporary hide/show)
    if (ModEntry.ModConfig.MachineProcessingIconsMode != 1)
    {
      return;
    }

    _helper.Input.SuppressActiveKeybinds(keybind);
    _toggleState.Value = !_toggleState.Value;
    ModEntry.ModConfig.MachineProcessingIconsVisible = _toggleState.Value;
    ToggleOption(_toggleState.Value);
  }

  private void OnSaving(object? sender, SavingEventArgs e)
  {
    ModEntry.ModConfig.MachineProcessingIconsVisible = _toggleState.Value;
    ModEntry.SaveConfig();
  }

  private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
  {
    if (!e.IsMultipleOf(15))
    {
      return;
    }

    List<MachineIconData> machines = _visibleMachines.Value;
    List<FishPondIconData> fishPonds = _visibleFishPonds.Value;
    machines.Clear();
    fishPonds.Clear();

    // Hold mode: show only while keybind is held; Toggle mode: use current toggle state
    bool showMachines =
      ModEntry.ModConfig.MachineProcessingIconsMode == 2
        ? ModEntry.ModConfig.ToggleMachineProcessingIcons.IsDown()
        : _showMachineIcons;

    if (
      Game1.currentLocation == null
      || !UIElementUtils.IsRenderingNormally()
      || Game1.activeClickableMenu != null
    )
    {
      return;
    }

    // Viewport bounds in tile coordinates
    int startX = Game1.viewport.X / Game1.tileSize - 1;
    int startY = Game1.viewport.Y / Game1.tileSize - 1;
    int endX = (Game1.viewport.X + Game1.viewport.Width) / Game1.tileSize + 1;
    int endY = (Game1.viewport.Y + Game1.viewport.Height) / Game1.tileSize + 1;

    if (showMachines)
    {
      foreach ((Vector2 tile, Object obj) in Game1.currentLocation.Objects.Pairs)
      {
        int tileX = (int)tile.X;
        int tileY = (int)tile.Y;

        if (tileX < startX || tileX > endX || tileY < startY || tileY > endY)
        {
          continue;
        }

        if (
          !obj.bigCraftable.Value
          || obj.heldObject.Value == null
          || obj.readyForHarvest.Value
          || obj.MinutesUntilReady <= 0
          || obj.Name == "Heater"
        )
        {
          continue;
        }

        // Prefer the input item (preservedParentSheetIndex) over the output (heldObject).
        // For Wine/Juice/Jelly/Pickles, this shows the original fruit/vegetable instead of the output.
        // For machines without a preserved parent (Furnace, etc.), fall back to the output item.
        Object heldObject = obj.heldObject.Value;
        string? preservedId = heldObject.preservedParentSheetIndex.Value;

        // Bee house re-evaluates honey flavor from the nearest flower at collect (RecalculateOnCollect),
        // so recompute the flower live. No flower gives "-1" (wild honey), which falls through to no icon.
        if (obj.QualifiedItemId == BeeHouseQualifiedId)
        {
          preservedId = MachineDataUtility.GetNearbyFlowerItemId(obj) ?? "-1";
        }

        ParsedItemData? itemData = !string.IsNullOrEmpty(preservedId)
          ? ItemRegistry.GetData("(O)" + preservedId) ?? ItemRegistry.GetData(preservedId)
          : ItemRegistry.GetData(heldObject.QualifiedItemId);

        ParsedItemData? machineData = ItemRegistry.GetData(obj.QualifiedItemId);
        if (itemData == null || machineData == null)
        {
          continue;
        }

        int machineSpriteHeight = machineData.GetSourceRect().Height;
        CustomOffsets.TryGetValue(obj.Name, out Vector2 offset);
        machines.Add(new MachineIconData(tile, itemData, machineSpriteHeight, offset));
      }
    }

    // Fish ponds: show the fish species icon at the pond center
    if (_showFishPondIcons)
    {
      foreach (Building building in Game1.currentLocation.buildings)
      {
        if (
          building is not FishPond fishPond
          || fishPond.fishType.Value == null
          || fishPond.currentOccupants.Value <= 0
        )
        {
          continue;
        }

        Vector2 centerTile = fishPond.GetCenterTile();
        if (
          centerTile.X < startX
          || centerTile.X > endX
          || centerTile.Y < startY
          || centerTile.Y > endY
        )
        {
          continue;
        }

        ParsedItemData? fishData = ItemRegistry.GetData("(O)" + fishPond.fishType.Value);
        if (fishData == null)
        {
          continue;
        }

        fishPonds.Add(new FishPondIconData(centerTile, fishData));
      }
    }
  }

  private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
  {
    if (!UIElementUtils.IsRenderingNormally() || Game1.activeClickableMenu != null)
    {
      return;
    }

    List<MachineIconData> machines = _visibleMachines.Value;
    List<FishPondIconData> fishPonds = _visibleFishPonds.Value;
    if (machines.Count == 0 && fishPonds.Count == 0)
    {
      return;
    }

    SpriteBatch spriteBatch = Game1.spriteBatch;

    // Fish pond icons: draw fish species at pond center
    foreach (FishPondIconData pond in fishPonds)
    {
      Vector2 screenPos = Game1.GlobalToLocal(
        new Vector2(pond.CenterTile.X * Game1.tileSize, pond.CenterTile.Y * Game1.tileSize)
      );

      Texture2D texture = pond.FishData.GetTexture();
      Rectangle sourceRect = pond.FishData.GetSourceRect();

      // Center the icon on the tile
      float iconSize = sourceRect.Width * Game1.pixelZoom;
      Vector2 iconPos = new Vector2(
        screenPos.X + Game1.tileSize / 2f - iconSize / 2f,
        screenPos.Y + Game1.tileSize / 2f - iconSize / 2f
      );

      float outlineScale = Game1.pixelZoom + 2f / sourceRect.Width;
      spriteBatch.Draw(
        texture,
        iconPos - new Vector2(1f, 1f),
        sourceRect,
        Color.Black * 0.5f,
        0f,
        Vector2.Zero,
        outlineScale,
        SpriteEffects.None,
        1f
      );
      spriteBatch.Draw(
        texture,
        iconPos,
        sourceRect,
        Color.White * 0.9f,
        0f,
        Vector2.Zero,
        Game1.pixelZoom,
        SpriteEffects.None,
        1f
      );
    }

    // Machine icons: draw processing item on each machine
    foreach (MachineIconData machine in machines)
    {
      Vector2 screenPos = Game1.GlobalToLocal(
        new Vector2(machine.Tile.X * Game1.tileSize, machine.Tile.Y * Game1.tileSize)
      );

      // Center icon on the machine sprite.
      // Machine renders from (tileY + tileSize - spriteHeight) to (tileY + tileSize).
      int spriteHeight = machine.MachineSpriteHeight * Game1.pixelZoom;
      float machineCenterX = screenPos.X + Game1.tileSize / 2f;
      float machineCenterY = screenPos.Y + Game1.tileSize - spriteHeight / 2f;
      Vector2 iconPos = new Vector2(
        machineCenterX - 16f + machine.Offset.X,
        machineCenterY - 16f + machine.Offset.Y
      );

      Texture2D texture = machine.ItemData.GetTexture();
      Rectangle sourceRect = machine.ItemData.GetSourceRect();

      // Outline: 2px larger black silhouette centered behind the icon
      float outlineScale = 2f + 2f / sourceRect.Width;
      spriteBatch.Draw(
        texture,
        iconPos - new Vector2(1f, 1f),
        sourceRect,
        Color.Black * 0.5f,
        0f,
        Vector2.Zero,
        outlineScale,
        SpriteEffects.None,
        1f
      );
      spriteBatch.Draw(
        texture,
        iconPos,
        sourceRect,
        Color.White * 0.9f,
        0f,
        Vector2.Zero,
        2f,
        SpriteEffects.None,
        1f
      );
    }
  }

  private readonly record struct MachineIconData(
    Vector2 Tile,
    ParsedItemData ItemData,
    int MachineSpriteHeight,
    Vector2 Offset
  );

  private readonly record struct FishPondIconData(Vector2 CenterTile, ParsedItemData FishData);
}
