using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.FishPonds;
using StardewValley.GameData.Machines;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.TokenizableStrings;
using UIInfoSuite2Alt.Compatibility;
using UIInfoSuite2Alt.Compatibility.Helpers;
using UIInfoSuite2Alt.Infrastructure;
using UIInfoSuite2Alt.Infrastructure.Extensions;
using UIInfoSuite2Alt.Infrastructure.Helpers;
using Object = StardewValley.Object;

namespace UIInfoSuite2Alt.UIElements;

internal readonly struct HoverSegment
{
  public string Text { get; }
  public Color? Color { get; }
  public Texture2D? Texture { get; }
  public Rectangle? SourceRect { get; }
  public float SpriteScale { get; }

  public HoverSegment(string text, Color? color = null)
  {
    Text = text;
    Color = color;
  }

  public HoverSegment(
    Texture2D texture,
    Rectangle sourceRect,
    float spriteScale,
    string text = "",
    Color? color = null
  )
  {
    Text = text;
    Color = color;
    Texture = texture;
    SourceRect = sourceRect;
    SpriteScale = spriteScale;
  }

  public bool HasSprite => Texture != null && SourceRect != null;

  public static implicit operator HoverSegment(string text) => new(text);
}

internal readonly struct HoverLine
{
  public IReadOnlyList<HoverSegment> Segments { get; }

  public HoverLine(string text, Color? color = null)
  {
    Segments = [new HoverSegment(text, color)];
  }

  public HoverLine(params HoverSegment[] segments)
  {
    Segments = segments;
  }

  public static implicit operator HoverLine(string text) => new(text);
}

internal class ShowTileTooltips : IDisposable
{
  private const int MAX_TREE_GROWTH_STAGE = 5;

  // Colors for the different tooltip text
  private static readonly Color ReadyColor = Tools.TooltipGreen;
  private static readonly Color WaitingColor = Tools.TooltipYellow;
  private static readonly Color WateredColor = Tools.TooltipBlue;
  private static readonly Color NotWateredColor = Tools.TooltipRed;

  private static readonly List<Func<Building?, List<HoverLine>, bool>> BuildingDetailRenderers =
    new() { DetailRenderers.BuildingOutput };

  private static readonly List<Func<Object?, List<HoverLine>, bool>> MachineDetailRenderers = new()
  {
    DetailRenderers.MachineTime,
  };

  private static readonly List<Func<TerrainFeature?, List<HoverLine>, bool>> CropDetailRenderers =
    new() { DetailRenderers.CropRender };

  private readonly PerScreen<TerrainFeature?> _currentTerrain = new();
  private readonly PerScreen<Object?> _currentTile = new();
  private readonly PerScreen<Building?> _currentTileBuilding = new();

  private readonly IModHelper _helper;
  private readonly ShowItemEffectRanges _itemEffectRanges;
  private readonly Lazy<Texture2D> _wildTreeTexture;
  private bool ShowCropTooltip => ModEntry.ModConfig.ShowCropTooltip;
  private bool ShowTreeTooltip => ModEntry.ModConfig.ShowTreeTooltip;
  private bool ShowBarrelTooltip => ModEntry.ModConfig.ShowBarrelTooltip;
  private bool ShowFishPondTooltip => ModEntry.ModConfig.ShowFishPondTooltip;
  private bool ShowForageableTooltip => ModEntry.ModConfig.ShowForageableTooltip;
  private bool ShowHarvestQuality => ModEntry.ModConfig.ShowHarvestQuality;

  public ShowTileTooltips(IModHelper helper, ShowItemEffectRanges itemEffectRanges)
  {
    _helper = helper;
    _itemEffectRanges = itemEffectRanges;

    _wildTreeTexture = new Lazy<Texture2D>(() =>
      AssetHelper.TryLoadTexture(_helper, "assets/wild_tree_tooltip.png")
    );
  }

  public void Dispose()
  {
    ToggleOption(false);
  }

  public void ToggleOption(bool enabled)
  {
    _helper.Events.Display.RenderingHud -= OnRenderingHud;
    _helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;

    if (!enabled)
    {
      return;
    }

    _helper.Events.Display.RenderingHud += OnRenderingHud;
    _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
  }

  private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
  {
    if (!e.IsMultipleOf(4))
    {
      return;
    }

    _currentTileBuilding.Value = null;
    _currentTile.Value = null;
    _currentTerrain.Value = null;

    Vector2 gamepadTile =
      Game1.player.CurrentTool != null
        ? Utility.snapToInt(Game1.player.GetToolLocation() / Game1.tileSize)
        : Utility.snapToInt(Game1.player.GetGrabTile());
    Vector2 mouseTile = Game1.currentCursorTile;

    Vector2 tile =
      Game1.options.gamepadControls && Game1.timerUntilMouseFade <= 0 ? gamepadTile : mouseTile;

    if (Game1.currentLocation == null)
    {
      return;
    }

    if (Game1.currentLocation.IsBuildableLocation())
    {
      _currentTileBuilding.Value = Game1.currentLocation.getBuildingAt(tile);
    }

    if (Game1.currentLocation.Objects?.TryGetValue(tile, out Object? currentObject) ?? false)
    {
      _currentTile.Value = currentObject;
    }

    if (
      Game1.currentLocation.terrainFeatures?.TryGetValue(tile, out TerrainFeature? terrain) ?? false
    )
    {
      _currentTerrain.Value = terrain;
    }

    if (_currentTile.Value is IndoorPot pot)
    {
      if (pot.hoeDirt.Value != null)
      {
        _currentTerrain.Value = pot.hoeDirt.Value;
      }

      if (pot.bush.Value != null)
      {
        _currentTerrain.Value = pot.bush.Value;
      }
    }
  }

  private void OnRenderingHud(object? sender, RenderingHudEventArgs e)
  {
    if (!UIElementUtils.IsRenderingNormally() || Game1.activeClickableMenu != null)
    {
      return;
    }

    List<HoverLine> lines = new();
    Vector2 tile = Vector2.Zero;
    Building? currentTileBuilding = _currentTileBuilding.Value;
    Object? currentTile = _currentTile.Value;
    TerrainFeature? terrain = _currentTerrain.Value;

    int overrideX = -1;
    int overrideY = -1;
    int predictedQuality = -1;

    if (
      ShowBarrelTooltip
      && !InformantHelper.IsFeatureEnabled("machine")
      && currentTileBuilding is not null
    )
    {
      foreach (
        Func<Building?, List<HoverLine>, bool> buildingDetailRenderer in BuildingDetailRenderers
      )
      {
        if (!buildingDetailRenderer(currentTileBuilding, lines))
        {
          continue;
        }

        Vector2 buildingTile = new(
          currentTileBuilding.tileX.Value,
          currentTileBuilding.tileY.Value
        );
        tile = Utility.ModifyCoordinatesForUIScale(
          Game1.GlobalToLocal(buildingTile * Game1.tileSize)
        );
      }
    }

    if (ShowFishPondTooltip && currentTileBuilding is FishPond fishPond)
    {
      if (DetailRenderers.FishPondRender(fishPond, lines))
      {
        Vector2 buildingTile = new(fishPond.tileX.Value, fishPond.tileY.Value);
        tile = Utility.ModifyCoordinatesForUIScale(
          Game1.GlobalToLocal(buildingTile * Game1.tileSize)
        );
      }
    }

    // Skip machine tooltip if Informant handles machines, or if a tree occupies this tile
    // and Informant handles trees (Informant's tree tooltip covers tappers on trees)
    bool informantCoversThisMachine =
      InformantHelper.IsFeatureEnabled("machine")
      || (terrain is Tree && InformantHelper.IsFeatureEnabled("tree"));

    if (
      ShowBarrelTooltip
      && !informantCoversThisMachine
      && currentTile is not null
      && !_itemEffectRanges.IsRangeTooltipActive
    )
    {
      foreach (Func<Object?, List<HoverLine>, bool> machineDetailRenderer in MachineDetailRenderers)
      {
        if (machineDetailRenderer(currentTile, lines))
        {
          tile = Utility.ModifyCoordinatesForUIScale(
            Game1.GlobalToLocal(
              new Vector2(currentTile.TileLocation.X, currentTile.TileLocation.Y) * Game1.tileSize
            )
          );
        }
      }

      // Show current quality star on the cask's held item icon
      if (currentTile is Cask { heldObject.Value: not null } caskTile)
      {
        predictedQuality = caskTile.heldObject.Value.Quality;
      }
    }

    // Ground forageables (leeks, daffodils, etc.) - show item name with sprite
    if (
      ShowForageableTooltip
      && currentTile is not null
      && !currentTile.bigCraftable.Value
      && currentTile.isForage()
      && lines.Count == 0
    )
    {
      lines.Add(new HoverLine(currentTile.DisplayName));
      tile = Utility.ModifyCoordinatesForUIScale(
        Game1.GlobalToLocal(currentTile.TileLocation * Game1.tileSize)
      );
      if (ShowHarvestQuality)
      {
        predictedQuality = QualityPrediction.PredictForageableQuality(
          currentTile.TileLocation.X,
          currentTile.TileLocation.Y,
          Game1.player
        );
      }
    }

    // Ground minerals in caves/mines - show item name with sprite, quality if WoL Gemologist
    if (
      ShowForageableTooltip
      && currentTile is not null
      && !currentTile.bigCraftable.Value
      && currentTile.IsSpawnedObject
      && !currentTile.isForage()
      && (
        currentTile.Category == Object.GemCategory
        || currentTile.Category == Object.mineralsCategory
      )
      && lines.Count == 0
    )
    {
      lines.Add(new HoverLine(currentTile.DisplayName));
      tile = Utility.ModifyCoordinatesForUIScale(
        Game1.GlobalToLocal(currentTile.TileLocation * Game1.tileSize)
      );
      if (ShowHarvestQuality)
      {
        predictedQuality = QualityPrediction.GetGemologistMineralQuality(Game1.player);
      }
    }

    if (ShowCropTooltip && !InformantHelper.IsFeatureEnabled("crop") && terrain is not null)
    {
      foreach (
        Func<TerrainFeature?, List<HoverLine>, bool> cropDetailRenderer in CropDetailRenderers
      )
      {
        if (cropDetailRenderer(terrain, lines))
        {
          tile = Utility.ModifyCoordinatesForUIScale(
            Game1.GlobalToLocal(terrain.Tile * Game1.tileSize)
          );
          if (ShowHarvestQuality && terrain is HoeDirt cropSoil)
          {
            predictedQuality = QualityPrediction.PredictCropOnTile(
              cropSoil,
              (int)terrain.Tile.X,
              (int)terrain.Tile.Y
            );
          }
        }
      }
    }

    if (ShowTreeTooltip && terrain is not null && !_itemEffectRanges.IsRangeTooltipActive)
    {
      // Skip tree tooltip if a tapper/machine is on this tile and Informant handles machines
      bool hasMachineOverlap =
        currentTile is not null
        && currentTile.bigCraftable.Value
        && InformantHelper.IsFeatureEnabled("machine");

      if (
        !hasMachineOverlap
        && !InformantHelper.IsFeatureEnabled("tree")
        && DetailRenderers.TreeRender(terrain, lines)
      )
      {
        tile = Utility.ModifyCoordinatesForUIScale(
          Game1.GlobalToLocal(terrain.Tile * Game1.tileSize)
        );
      }

      if (
        !InformantHelper.IsFeatureEnabled("fruit-tree")
        && DetailRenderers.FruitTreeRender(terrain, lines)
      )
      {
        tile = Utility.ModifyCoordinatesForUIScale(
          Game1.GlobalToLocal(terrain.Tile * Game1.tileSize)
        );
      }

      if (!InformantHelper.IsFeatureEnabled("tea-bush") && DetailRenderers.TeaBush(terrain, lines))
      {
        tile = Utility.ModifyCoordinatesForUIScale(
          Game1.GlobalToLocal(terrain.Tile * Game1.tileSize)
        );
      }
    }

    if (lines.Count <= 0)
    {
      return;
    }

    if (Game1.options.gamepadControls && Game1.timerUntilMouseFade <= 0)
    {
      overrideX = (int)(tile.X + Utility.ModifyCoordinateForUIScale(32));
      overrideY = (int)(tile.Y + Utility.ModifyCoordinateForUIScale(32));
    }

    (Texture2D? spriteTexture, Rectangle? spriteSourceRect) = GetTooltipSprite(
      currentTile,
      currentTileBuilding,
      terrain
    );
    DrawColoredHoverText(
      Game1.spriteBatch,
      lines,
      Game1.smallFont,
      overrideX,
      overrideY,
      spriteTexture,
      spriteSourceRect,
      predictedQuality
    );
  }

  private (Texture2D? Texture, Rectangle? SourceRect) GetTooltipSprite(
    Object? tileObject,
    Building? building,
    TerrainFeature? terrain
  )
  {
    // Machine: show the output item (e.g. Wine, Juice) - complements the machine icon which shows the input
    if (
      tileObject != null
      && tileObject.bigCraftable.Value
      && tileObject.heldObject.Value != null
      && tileObject.MinutesUntilReady > 0
    )
    {
      return FromItemData(ItemRegistry.GetData(tileObject.heldObject.Value.QualifiedItemId));
    }

    // Building: prefer output item (e.g. Flour from a Mill), fall back to first input
    if (building != null && building is not FishPond)
    {
      List<Item?> inputItems = new();
      List<Item?> outputItems = new();
      MachineHelper.GetBuildingChestItems(building, inputItems, outputItems);
      Item? firstItem =
        outputItems.FirstOrDefault(i => i != null) ?? inputItems.FirstOrDefault(i => i != null);
      if (firstItem != null)
      {
        return FromItemData(ItemRegistry.GetData(firstItem.QualifiedItemId));
      }
    }

    // Fish pond: show the fish
    if (
      building is FishPond fishPond
      && fishPond.fishType.Value != null
      && fishPond.currentOccupants.Value > 0
    )
    {
      return FromItemData(ItemRegistry.GetData(fishPond.GetFishObject().QualifiedItemId));
    }

    // Ground forageable or mineral: show the item itself
    if (
      tileObject is not null
      && !tileObject.bigCraftable.Value
      && (
        tileObject.isForage()
        || (
          tileObject.IsSpawnedObject
          && (
            tileObject.Category == Object.GemCategory
            || tileObject.Category == Object.mineralsCategory
          )
        )
      )
    )
    {
      return FromItemData(ItemRegistry.GetData(tileObject.QualifiedItemId));
    }

    if (terrain == null)
    {
      return (null, null);
    }

    // Crop: show harvest item
    if (terrain is HoeDirt hoeDirt && hoeDirt.crop is { dead.Value: false })
    {
      Crop crop = hoeDirt.crop;
      if (crop.forageCrop.Value)
      {
        string forageCropItemId = crop.whichForageCrop.Value switch
        {
          "1" => "(O)399",
          "2" => "(O)829",
          _ => $"(O){crop.whichForageCrop.Value}",
        };
        return FromItemData(ItemRegistry.GetData(forageCropItemId));
      }

      if (crop.indexOfHarvest.Value != null)
      {
        string itemId = crop.isWildSeedCrop()
          ? crop.whichForageCrop.Value
          : crop.indexOfHarvest.Value;
        return FromItemData(ItemRegistry.GetData($"(O){itemId}"));
      }
    }

    // Fruit tree: show the sapling sprite
    if (terrain is FruitTree fruitTree)
    {
      return FromItemData(ItemRegistry.GetData(fruitTree.treeId.Value));
    }

    // Wild tree: bundled asset to avoid conflicts with mods that replace profession icons on Cursors
    if (terrain is Tree)
    {
      return (_wildTreeTexture.Value, new Rectangle(0, 0, 12, 16));
    }

    // Tea/custom bush: show drop item
    if (terrain is Bush bush && bush.size.Value == Bush.greenTeaBush)
    {
      if (
        ApiManager.GetApi(ModCompat.CustomBush, out ICustomBushApi? customBushApi)
        && customBushApi.TryGetBush(bush, out ICustomBushData? customBushData, out string? id)
      )
      {
        if (customBushApi.TryGetShakeOffItem(bush, out Item? shakeOffItem))
        {
          return FromItemData(ItemRegistry.GetData(shakeOffItem.QualifiedItemId));
        }

        List<PossibleDroppedItem> drops = customBushApi.GetCustomBushDropItems(customBushData, id);
        if (drops.Count > 0)
        {
          return FromItemData(drops[0].Item);
        }
      }
      else
      {
        return FromItemData(ItemRegistry.GetData("(O)815")); // Tea Leaves
      }
    }

    return (null, null);

    static (Texture2D? Texture, Rectangle? SourceRect) FromItemData(ParsedItemData? data)
    {
      return data != null ? (data.GetTexture(), data.GetSourceRect()) : (null, null);
    }
  }

  private static void DrawColoredHoverText(
    SpriteBatch b,
    List<HoverLine> lines,
    SpriteFont font,
    int overrideX = -1,
    int overrideY = -1,
    Texture2D? spriteTexture = null,
    Rectangle? spriteSourceRect = null,
    int quality = -1
  )
  {
    const int spriteSize = 32;
    const int spritePadding = 4;
    bool hasSprite = spriteTexture != null && spriteSourceRect != null;
    float spriteScale = hasSprite
      ? spriteSize / (float)Math.Max(spriteSourceRect!.Value.Width, spriteSourceRect.Value.Height)
      : 0;
    int renderedSpriteWidth = hasSprite ? (int)(spriteSourceRect!.Value.Width * spriteScale) : 0;
    int spriteSpace = hasSprite ? renderedSpriteWidth + spritePadding : 0;

    float maxWidth = 0;
    bool isFirstLine = true;
    foreach (HoverLine line in lines)
    {
      float lineWidth = 0;
      foreach (HoverSegment segment in line.Segments)
      {
        if (segment.HasSprite)
        {
          lineWidth += segment.SourceRect!.Value.Width * segment.SpriteScale;
        }

        if (segment.Text.Length > 0)
        {
          lineWidth += font.MeasureString(segment.Text).X;
        }
      }

      // First line needs extra room for the sprite
      if (isFirstLine)
      {
        lineWidth += spriteSpace;
        isFirstLine = false;
      }

      maxWidth = Math.Max(maxWidth, lineWidth);
    }

    int width = (int)maxWidth + 32;
    int height = Math.Max(66, lines.Count * font.LineSpacing + 38);

    int x = Game1.getOldMouseX() + 32;
    int y = Game1.getOldMouseY() + 32;

    if (overrideX != -1)
    {
      x = overrideX;
    }

    if (overrideY != -1)
    {
      y = overrideY;
    }

    Rectangle safeArea = Utility.getSafeArea();
    if (x + width > safeArea.Right)
    {
      x = safeArea.Right - width;
      y += 16;
    }

    if (y + height > safeArea.Bottom)
    {
      x += 16;
      if (x + width > safeArea.Right)
      {
        x = safeArea.Right - width;
      }

      y = safeArea.Bottom - height;
    }

    width += 4;

    IClickableMenu.drawTextureBox(
      b,
      Game1.menuTexture,
      new Rectangle(0, 256, 60, 60),
      x,
      y,
      width,
      height,
      Color.White
    );

    Color defaultColor = Game1.textColor;
    Color shadowColor = Game1.textShadowColor;
    float lineY = y + 16 + 4;
    bool isFirst = true;

    foreach (HoverLine line in lines)
    {
      float segX = x + 16;

      // Indent first line to leave room for the sprite
      if (isFirst && hasSprite)
      {
        segX += spriteSpace;
      }

      foreach (HoverSegment segment in line.Segments)
      {
        if (segment.HasSprite)
        {
          Rectangle srcRect = segment.SourceRect!.Value;
          float spriteW = srcRect.Width * segment.SpriteScale;
          float spriteH = srcRect.Height * segment.SpriteScale;
          float spriteCY = lineY + font.LineSpacing / 2f - spriteH / 2f;
          b.Draw(
            segment.Texture!,
            new Vector2(segX, spriteCY),
            srcRect,
            Color.White,
            0f,
            Vector2.Zero,
            segment.SpriteScale,
            SpriteEffects.None,
            0.91f
          );
          segX += spriteW;
        }

        if (segment.Text.Length > 0)
        {
          Color segColor = segment.Color ?? defaultColor;
          Vector2 pos = new(segX, lineY);
          Tools.DrawShadowedText(b, font, segment.Text, pos, segColor, shadowColor);
          segX += font.MeasureString(segment.Text).X;
        }
      }

      // Draw sprite on the first line, vertically centered with the text
      if (isFirst && hasSprite)
      {
        Rectangle sourceRect = spriteSourceRect!.Value;
        float scale = spriteSize / (float)Math.Max(sourceRect.Width, sourceRect.Height);
        float spriteCenterY = lineY + font.LineSpacing / 2f - (sourceRect.Height * scale) / 2f - 2f;
        Vector2 spritePos = new(x + 16, spriteCenterY);
        b.Draw(
          spriteTexture!,
          spritePos,
          sourceRect,
          Color.White,
          0f,
          Vector2.Zero,
          scale,
          SpriteEffects.None,
          0.9f
        );

        // Draw quality star at bottom-left of sprite, slightly overlapping
        if (quality > 0)
        {
          Rectangle qualityRect =
            quality < 4
              ? new Rectangle(338 + (quality - 1) * 8, 400, 8, 8)
              : new Rectangle(346, 392, 8, 8);
          float qualityScale = 2f;
          float iridiumPulse =
            quality >= 4
              ? (
                (float)Math.Cos(Game1.currentGameTime.TotalGameTime.Milliseconds * Math.PI / 512.0)
                + 1f
              ) * 0.05f
              : 0f;
          Vector2 qualityPos = new(spritePos.X + 6f, spritePos.Y + sourceRect.Height * scale - 8f);
          b.Draw(
            Game1.mouseCursors,
            qualityPos,
            qualityRect,
            Color.White,
            0f,
            new Vector2(4f, 4f),
            qualityScale * (1f + iridiumPulse),
            SpriteEffects.None,
            0.91f
          );
        }

        isFirst = false;
      }

      lineY += font.LineSpacing;
    }
  }

  private static IEnumerable<string> GetFertilizerList(HoeDirt dirtTile)
  {
    if (string.IsNullOrWhiteSpace(dirtTile.fertilizer.Value))
      return [];

    var fertilizerNames = new Dictionary<string, int>();

    // Supports Ultimate Fertilizer's pipe-delimited format
    foreach (string fertilizerStr in dirtTile.fertilizer.Value.Split('|'))
    {
      string name = ItemRegistry.GetData(fertilizerStr)?.DisplayName ?? "Unknown Fertilizer";
      int count = fertilizerNames.GetValueOrDefault(name, 0);
      fertilizerNames[name] = count + 1;
    }

    return fertilizerNames
      .OrderBy(kv => kv.Value)
      .ThenBy(kv => kv.Key)
      .Select(kv =>
      {
        string quantityStr = kv.Value == 1 ? "" : $" x{kv.Value}";
        return $"{kv.Key}{quantityStr}";
      });
  }

  // See: stardewvalleywiki.com/Trees
  internal static string GetTreeTypeName(string treeType)
  {
    switch (treeType)
    {
      case "1":
        return I18n.Oak();
      case "2":
        return I18n.Maple();
      case "3":
        return I18n.Pine();
      case "6":
        return I18n.Palm();
      case "7":
        return I18n.Mushroom();
      case "8":
        return I18n.Mahogany();
      case "9":
        return I18n.PalmJungle();
      case "10":
        return I18n.GreenRainType1();
      case "11":
        return I18n.GreenRainType2();
      case "12":
        return I18n.GreenRainType3();
      case "13":
        return I18n.Mystic();
      case "Lumisteria.MtVapius.Birchtree":
        return I18n.VmvBirch();
      case "Lumisteria.MtVapius.HazelnutTree":
        return I18n.VmvHazelnut();
      case "Lumisteria.MtVapius.SkyshardPineTree":
        return I18n.VmvSkyshardPine();
      case "Lumisteria.MtVapius.AmberTree":
        return I18n.VmvAmber();
      case "Lumisteria.MtVapius.BlackChanterelleTree":
        return I18n.VmvBlackChanterelle();
      case "FlashShifter.StardewValleyExpandedCP_Birch_Tree":
        return I18n.SVEBirch();
      case "FlashShifter.StardewValleyExpandedCP_Fir_Tree":
        return I18n.SVEFir();
      case "Cornucopia_SapodillaSeed":
        return I18n.CORSapodilla();
      case "Cornucopia_CorpseFlowerSeed":
        return I18n.CORCorpseFlower();
      case "Cornucopia_DatePalmSeed":
        return I18n.CORDatePalm();
      case "skellady.SBVCP.CinderTree":
        return I18n.SbvCinder();
      case "Wildflour.SASS_Stout_Funnel_Tree":
        return I18n.SbvStoutFunnel();
      case "Wildflour.SASS_Sparkling_Agaric_Tree":
        return I18n.SbvSparklingAgaric();
      case "Wildflour.SASS_Seafoam_Waxcap_Tree":
        return I18n.SbvSeafoamWaxcap();
      case "Wildflour.SASS_Lunar_Poof_Tree":
        return I18n.SbvLunarPoof();
      case "Wildflour.SASS_Indigo_Cap_Tree":
        return I18n.SbvIndigoCap();
      case "Wildflour.SASS_Lilac_Funnel_Tree":
        return I18n.SbvLilacFunnel();
      case "Wildflour.SASS_Limey_Bonnet_Tree":
        return I18n.SbvLimeyBonnet();
      case "Wildflour.SASS_Coral_Fungus_Tree":
        return I18n.SbvCoralFungus();
      case "Wildflour.SASS_Ghostly_Parasol_Tree":
        return I18n.SbvGhostlyParasol();
      case "Wildflour.SASS_Frilly_Gilly_Tree":
        return I18n.SbvFrillyGilly();
      default:
        ModEntry.MonitorObject.LogOnce(
          $"ShowTileTooltips: unknown tree type, treeType={treeType}",
          LogLevel.Warn
        );
        return $"Unknown (#{treeType})";
    }
  }

  private static class DetailRenderers
  {
    private static HoverLine GetInfoStringForDrop(PossibleDroppedItem item)
    {
      (int nextDayToProduce, ParsedItemData? parsedItemData, float chance, string? _) = item;

      string chanceStr = 1.0f.Equals(chance) ? "" : $" ({chance * 100:2F}%)";
      int daysUntilReady = nextDayToProduce - Game1.dayOfMonth;
      return daysUntilReady <= 0
        ? new HoverLine(
          $"{parsedItemData.DisplayName}: ",
          new HoverSegment(I18n.ReadyToHarvest(), ReadyColor)
        )
        : new HoverLine(
          $"{parsedItemData.DisplayName}: ",
          new HoverSegment($"{daysUntilReady} {I18n.Days()}{chanceStr}", WaitingColor)
        );
    }

    private static Dictionary<string, int> GetItemCountMap(List<Item?> items)
    {
      Dictionary<string, int> itemCounter = new();
      foreach (Item? outputItem in items)
      {
        if (outputItem is null)
        {
          continue;
        }

        int count = itemCounter.GetOrDefault(outputItem.DisplayName, 0) + outputItem.Stack;
        itemCounter[outputItem.DisplayName] = count;
      }

      return itemCounter;
    }

    public static bool FishPondRender(FishPond fishPond, List<HoverLine> entries)
    {
      if (fishPond.fishType.Value == null || fishPond.currentOccupants.Value <= 0)
      {
        return false;
      }

      // Fish name
      string fishName = fishPond.GetFishObject().DisplayName;
      entries.Add(fishName);

      // Population: current/max
      int current = fishPond.currentOccupants.Value;
      int max = fishPond.maxOccupants.Value;
      Color populationColor = current >= max ? ReadyColor : WaitingColor;
      entries.Add(new HoverLine(I18n.FishPondPopulation(current, max: max), populationColor));

      // Quest item needed
      if (fishPond.neededItem.Value != null && fishPond.HasUnresolvedNeeds())
      {
        string itemName = fishPond.neededItem.Value.DisplayName;
        int itemCount = fishPond.neededItemCount.Value;
        entries.Add(
          new HoverLine(I18n.FishPondQuestItem(itemName, count: itemCount), WaitingColor)
        );
      }

      // Next spawn / quest timing
      FishPondData? pondData = fishPond.GetFishPondData();
      if (pondData != null)
      {
        int daysUntilSpawn = pondData.SpawnTime - fishPond.daysSinceSpawn.Value;

        if (current < max && !fishPond.hasSpawnedFish.Value && daysUntilSpawn > 0)
        {
          // Not at max — show days until next fish spawns
          entries.Add(new HoverLine(I18n.FishPondNextSpawn(daysUntilSpawn), WaitingColor));
        }
        else if (
          current >= max
          && fishPond.neededItem.Value == null
          && daysUntilSpawn > 0
          && pondData.PopulationGates != null
          && pondData.PopulationGates.ContainsKey(max + 1)
        )
        {
          // At max, no quest yet, but a gate exists — show days until quest appears
          entries.Add(new HoverLine(I18n.FishPondNextQuest(daysUntilSpawn), WaitingColor));
        }
      }

      // Golden Animal Cracker
      if (fishPond.goldenAnimalCracker.Value)
      {
        entries.Add(new HoverLine(I18n.FishPondGoldenCracker(), WaitingColor));
      }

      return true;
    }

    public static bool BuildingOutput(Building? building, List<HoverLine> entries)
    {
      if (building is null)
      {
        return false;
      }

      List<Item?> inputItems = new();
      List<Item?> outputItems = new();
      MachineHelper.GetBuildingChestItems(building, inputItems, outputItems);

      Dictionary<string, int> inputItemsMap = GetItemCountMap(inputItems);
      Dictionary<string, int> outputItemsMap = GetItemCountMap(outputItems);

      if (inputItemsMap.Count > 0)
      {
        entries.Add($"{I18n.MachineProcessing()}:");
        foreach ((string displayName, int count) in inputItemsMap)
        {
          entries.Add($"{displayName} x{count}");
        }
      }

      if (outputItemsMap.Count <= 0)
      {
        return true;
      }

      if (inputItemsMap.Count > 0)
      {
        entries.Add("");
      }

      entries.Add($"{I18n.MachineDone()}:");
      foreach ((string displayName, int count) in outputItemsMap)
      {
        entries.Add($"{displayName} x{count}");
      }

      return true;
    }

    public static bool MachineTime(Object? tileObject, List<HoverLine> entries)
    {
      if (
        tileObject == null
        || !tileObject.bigCraftable.Value
        || tileObject.MinutesUntilReady <= 0
        || tileObject.heldObject.Value == null
        || tileObject.Name == "Heater"
      )
      {
        return false;
      }

      entries.Add(tileObject.heldObject.Value.DisplayName);
      if (tileObject is Cask cask)
      {
        AddCaskAgingLines(cask, entries);
        return true;
      }

      // Machines using DaysUntilReady always finish at morning regardless of when loaded,
      // so a minute-based countdown is misleading. Show "Ready: X day(s)" instead.
      int daysUntilReady = GetDaysUntilReady(tileObject);
      if (daysUntilReady >= 0)
      {
        int daysLeft = (tileObject.MinutesUntilReady + 1599) / 1600;
        string daysText = daysLeft <= 1 ? I18n.MachineReadyTomorrow() : $"{daysLeft} {I18n.Days()}";
        entries.Add(
          new HoverLine(
            new HoverSegment(I18n.MachineReadyPrefix()),
            new HoverSegment(daysText, WaitingColor)
          )
        );
        return true;
      }

      int timeLeft = tileObject.MinutesUntilReady;
      int longTime = timeLeft / 60;
      string longText = I18n.Hours();
      int shortTime = timeLeft % 60;
      string shortText = I18n.Minutes();

      // ~1600 minutes per day — approximate since overnight time varies
      if (timeLeft >= 1600)
      {
        longText = I18n.Days();
        longTime = timeLeft / 1600;

        shortText = I18n.Hours();
        shortTime = timeLeft % 1600;

        // Fudged: 60min/hr daytime, 100min/hr overnight — prevents "25 hours" display
        if (shortTime <= 1200)
        {
          shortTime /= 60;
        }
        else
        {
          shortTime = 20 + (shortTime - 1200) / 100;
        }
      }

      StringBuilder builder = new();

      if (longTime > 0)
      {
        builder.Append($"{longTime} {longText}, ");
      }

      builder.Append($"{shortTime} {shortText}");
      entries.Add(builder.ToString());
      return true;
    }

    private static readonly (int Quality, float DaysThreshold)[] CaskQualityStages =
    [
      (1, 42f), // Silver
      (2, 28f), // Gold
      (4, 0f), // Iridium
    ];

    private static void AddCaskAgingLines(Cask cask, List<HoverLine> entries)
    {
      int currentQuality = cask.heldObject.Value?.Quality ?? 0;
      float daysToMature = cask.daysToMature.Value;
      float agingRate = cask.agingRate.Value;

      foreach ((int quality, float threshold) in CaskQualityStages)
      {
        if (quality <= currentQuality)
        {
          continue;
        }

        // Iridium uses value 4 but must also skip if current is gold (2) going to iridium (4)
        // — handled by quality <= currentQuality since 4 > 2

        int daysUntilThisQuality = (int)Math.Ceiling((daysToMature - threshold) / agingRate);
        if (daysUntilThisQuality <= 0)
        {
          continue;
        }

        Rectangle starRect =
          quality < 4
            ? new Rectangle(338 + (quality - 1) * 8, 400, 8, 8)
            : new Rectangle(346, 392, 8, 8);

        string daysText = daysUntilThisQuality == 1 ? I18n.Day() : I18n.Days();
        entries.Add(
          new HoverLine(
            new HoverSegment(Game1.mouseCursors, starRect, 2f, $" {I18n.CaskAgingIn()} "),
            new HoverSegment($"{daysUntilThisQuality} {daysText}", WaitingColor)
          )
        );
      }
    }

    /// <summary>
    /// Returns the DaysUntilReady value from the machine's last output rule, or -1 if
    /// the machine doesn't use day-based timing (i.e. uses MinutesUntilReady instead).
    /// </summary>
    private static int GetDaysUntilReady(Object machine)
    {
      MachineData? machineData = machine.GetMachineData();
      string? ruleId = machine.lastOutputRuleId.Value;
      if (machineData?.OutputRules == null || ruleId == null)
      {
        return -1;
      }

      foreach (MachineOutputRule rule in machineData.OutputRules)
      {
        if (rule.Id == ruleId)
        {
          return rule.DaysUntilReady;
        }
      }

      return -1;
    }

    public static bool CropRender(TerrainFeature? terrain, List<HoverLine> entries)
    {
      if (terrain is not HoeDirt hoeDirt)
      {
        return false;
      }

      IEnumerable<string> fertilizers = [];

      if (!string.IsNullOrEmpty(hoeDirt.fertilizer.Value) && !"0".Equals(hoeDirt.fertilizer.Value))
      {
        fertilizers = GetFertilizerList(hoeDirt);
      }

      if (hoeDirt.crop is not null && !hoeDirt.crop.dead.Value)
      {
        Crop crop = hoeDirt.crop;
        var daysLeft = 0;

        if (hoeDirt.crop.fullyGrown.Value)
        {
          daysLeft = Math.Max(0, hoeDirt.crop.dayOfCurrentPhase.Value);
        }
        else
        {
          for (int i = hoeDirt.crop.currentPhase.Value; i < hoeDirt.crop.phaseDays.Count - 1; i++)
          {
            daysLeft += hoeDirt.crop.phaseDays[i];
          }
          daysLeft -= hoeDirt.crop.dayOfCurrentPhase.Value;
        }

        string cropName = DropsHelper.GetCropHarvestName(crop);

        if (crop.forageCrop.Value)
        {
          // Forage crops (spring onion, ginger) are always ready
          entries.Add(new HoverLine(cropName));
        }
        else
        {
          string daysLeftStr = daysLeft <= 0 ? I18n.ReadyToHarvest() : $"{daysLeft} {I18n.Days()}";
          Color cropColor = daysLeft <= 0 ? ReadyColor : WaitingColor;
          entries.Add(new HoverLine($"{cropName}: ", new HoverSegment(daysLeftStr, cropColor)));

          bool isWatered = hoeDirt.state.Value == 1;
          string waterStatus = isWatered ? I18n.Watered() : I18n.NotWatered();
          Color waterColor = isWatered ? WateredColor : NotWateredColor;
          entries.Add(new HoverLine(waterStatus, waterColor));
        }
      }

      if (fertilizers.Any())
      {
        var fertList = fertilizers.ToList();

        for (int i = 0; i < fertList.Count; i++)
        {
          string currentName = fertList[i];
          string lineText;

          if (fertList.Count == 1)
          {
            lineText = $"({I18n.With()} {currentName})";
          }
          else if (i == 0)
          {
            lineText = $"({I18n.With()} {currentName},";
          }
          else if (i == fertList.Count - 1)
          {
            lineText = $"{currentName})";
          }
          else
          {
            lineText = $"{currentName},";
          }
          entries.Add(new HoverLine(lineText));
        }
      }

      return true;
    }

    public static bool TreeRender(TerrainFeature? terrain, List<HoverLine> entries)
    {
      if (terrain is not Tree tree)
      {
        return false;
      }

      bool isStump = tree.stump.Value;
      string treeTypeName = GetTreeTypeName(tree.treeType.Value);
      string stumpText = isStump ? $" ({I18n.Stump()})" : "";
      entries.Add($"{treeTypeName}{I18n.Tree()}{stumpText}");

      if (tree.growthStage.Value >= MAX_TREE_GROWTH_STAGE)
      {
        return true;
      }

      entries.Add($"{I18n.Stage()} {tree.growthStage.Value} / {MAX_TREE_GROWTH_STAGE}");
      if (tree.fertilized.Value)
      {
        entries.Add($"({I18n.Fertilized()})");
      }

      return true;
    }

    public static bool FruitTreeRender(TerrainFeature? terrain, List<HoverLine> entries)
    {
      if (terrain is not FruitTree fruitTree)
      {
        return false;
      }

      FruitTreeInfo treeInfo = DropsHelper.GetFruitTreeInfo(fruitTree);
      entries.Add(treeInfo.TreeName);
      if (fruitTree.daysUntilMature.Value > 0)
      {
        entries.Add($"{fruitTree.daysUntilMature.Value} {I18n.DaysToMature()}");
        return true;
      }

      if (treeInfo.Items.Count <= 1)
      {
        return true;
      }

      entries.AddRange(treeInfo.Items.Select(GetInfoStringForDrop));
      return true;
    }

    public static bool TeaBush(TerrainFeature? terrain, List<HoverLine> entries)
    {
      if (terrain is not Bush bush || bush.size.Value != Bush.greenTeaBush)
      {
        return false;
      }

      var ageToMature = 20;
      bool willProduceThisSeason = Game1.season != Season.Winter || bush.IsSheltered();
      string bushName = ItemRegistry.GetData("(O)251").DisplayName;
      bool inProductionPeriod = Game1.dayOfMonth >= 22;
      int daysUntilProductionPeriod = inProductionPeriod ? 0 : 22 - Game1.dayOfMonth;
      List<PossibleDroppedItem> droppedItems = new();

      if (bush.tileSheetOffset.Value == 1)
      {
        droppedItems.Add(
          new PossibleDroppedItem(Game1.dayOfMonth, ItemRegistry.GetData("(O)815"), 1.0f)
        );
      }
      else if (Game1.dayOfMonth >= 21 && Game1.dayOfMonth < 28)
      {
        droppedItems.Add(
          new PossibleDroppedItem(Game1.dayOfMonth + 1, ItemRegistry.GetData("(O)815"), 1.0f)
        );
      }

      if (ApiManager.GetApi(ModCompat.CustomBush, out ICustomBushApi? customBushApi))
      {
        if (customBushApi.TryGetBush(bush, out ICustomBushData? customBushData, out string? id))
        {
          droppedItems.Clear();
          willProduceThisSeason = customBushApi.IsInSeason(bush);
          string displayName = customBushData.DisplayName;
          if (displayName.Contains("LocalizedText"))
          {
            displayName = TokenParser.ParseText(displayName);
          }

          ageToMature = customBushData.AgeToProduce;
          inProductionPeriod = Game1.dayOfMonth >= customBushData.DayToBeginProducing;
          daysUntilProductionPeriod = inProductionPeriod ? 0 : 22 - Game1.dayOfMonth;

          if (customBushApi.TryGetShakeOffItem(bush, out Item? shakeOffItem))
          {
            droppedItems.Add(
              new PossibleDroppedItem(
                Game1.dayOfMonth,
                ItemRegistry.GetData(shakeOffItem.QualifiedItemId),
                1.0f,
                id
              )
            );
          }
          else
          {
            droppedItems = customBushApi.GetCustomBushDropItems(customBushData, id);
          }

          // Single-drop bush: use item name as bush name
          if (droppedItems.Count == 1)
          {
            string suffix = bush.getAge() >= ageToMature ? "Bush" : "Sapling";
            bushName = $"{droppedItems[0].Item.DisplayName} {suffix}";
          }
          else
          {
            bushName = displayName;
          }
        }
      }

      entries.Add(bushName);
      bool isMature = bush.getAge() >= ageToMature;
      if (!isMature || !willProduceThisSeason)
      {
        if (!isMature)
        {
          entries.Add($"{ageToMature - bush.getAge()} {I18n.DaysToMature()}");
        }

        if (!willProduceThisSeason)
        {
          entries.Add(I18n.DoesNotProduceThisSeason());
        }

        return true;
      }

      // Not yet in production period
      if (!inProductionPeriod)
      {
        entries.Add($"{daysUntilProductionPeriod} {I18n.Days()}");
        return true;
      }

      entries.AddRange(droppedItems.Select(GetInfoStringForDrop));
      return true;
    }
  }
}
