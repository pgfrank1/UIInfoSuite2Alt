using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;
using UIInfoSuite2Alt.Compatibility;
using UIInfoSuite2Alt.Compatibility.Helpers;
using UIInfoSuite2Alt.Infrastructure;
using UIInfoSuite2Alt.Infrastructure.Extensions;
using UIInfoSuite2Alt.Infrastructure.Helpers;
using UIInfoSuite2Alt.Options;
using UIInfoSuite2Alt.UIElements.DonationIcons;
using Object = StardewValley.Object;

namespace UIInfoSuite2Alt.UIElements;

internal class ShowItemHoverInformation : IDisposable
{
  private readonly ClickableTextureComponent _bundleIcon = new(
    new Rectangle(0, 0, Game1.tileSize, Game1.tileSize),
    Game1.mouseCursors,
    new Rectangle(331, 374, 15, 14),
    3f
  );

  private readonly IModHelper _helper;

  private readonly Dictionary<int, Color?> _bundleColorCache = new();
  private readonly PerScreen<Item?> _hoverItem = new();

  private readonly DonationIconRow _donationIcons = new();
  private readonly MuseumDonationProvider _museumProvider = new();

  private (Texture2D texture, Rectangle sourceRect)? _ubIconOverride;

  private static readonly Rectangle CollectionsTabSourceRect = new(640, 80, 16, 17);
  private static readonly Rectangle ShippingBinBaseRect = new(526, 218, 30, 22);
  private static readonly Rectangle ShippingBinLidRect = new(134, 236, 30, 15);

  public ShowItemHoverInformation(IModHelper helper)
  {
    _helper = helper;

    _donationIcons.AddProvider(_museumProvider);
    _donationIcons.AddProvider(new AquariumDonationProvider(helper));
  }

  public void Dispose()
  {
    ToggleOption(false);
  }

  public void ToggleOption(bool showItemHoverInformation)
  {
    _helper.Events.Display.RenderedActiveMenu -= OnRenderedActiveMenu;
    _helper.Events.Display.RenderedHud -= OnRenderedHud;
    _helper.Events.Display.Rendering -= OnRendering;

    if (showItemHoverInformation)
    {
      _museumProvider.Initialize();
      ArtisanPriceHelper.EnsureInitialized(_helper);

      _helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
      _helper.Events.Display.RenderedHud += OnRenderedHud;
      _helper.Events.Display.Rendering += OnRendering;
    }
  }

  private void OnRendering(object? sender, EventArgs e)
  {
    _hoverItem.Value = Tools.GetHoveredItem();
  }

  private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
  {
    if (!Game1.displayHUD || Game1.eventUp || Game1.isFestival())
    {
      return;
    }

    if (Game1.activeClickableMenu == null)
    {
      DrawAdvancedTooltip(e.SpriteBatch);
    }
  }

  [EventPriority(EventPriority.Low)]
  private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
  {
    if (Game1.activeClickableMenu != null)
    {
      DrawAdvancedTooltip(e.SpriteBatch);
    }
  }

  private void DrawAdvancedTooltip(SpriteBatch spriteBatch)
  {
    if (
      _hoverItem.Value != null
      && !(_hoverItem.Value is MeleeWeapon weapon && weapon.isScythe())
      && _hoverItem.Value is not FishingRod
    )
    {
      ModConfig config = ModEntry.ModConfig;
      var hoveredObject = _hoverItem.Value as Object;

      bool showPrice = config.ShowInventoryItemSellPrice;
      bool showBundle = config.ShowInventoryItemBundleBanner;
      bool showDonation = config.ShowInventoryItemDonationStatus;
      bool showShipping = config.ShowInventoryItemShippingStatus;

      int itemPrice = showPrice ? Tools.GetSellToStorePrice(_hoverItem.Value) : 0;

      var stackPrice = 0;
      if (itemPrice > 0 && _hoverItem.Value.Stack > 1)
      {
        stackPrice = itemPrice * _hoverItem.Value.Stack;
      }

      int cropPrice = showPrice ? Tools.GetHarvestPrice(_hoverItem.Value) : 0;

      // Artisan good prices. Sub-option of ShowInventoryItemSellPrice.
      bool showArtisan = showPrice && config.ShowInventoryItemArtisanPrices;
      ArtisanPriceHelper.ArtisanEntry?[] artisanEntries =
        showArtisan && itemPrice > 0 ? ArtisanPriceHelper.GetEntries(_hoverItem.Value) : [];
      int artisanRowCount = 0;
      int artisanMaxTextWidth = 0;
      int hoverStack = _hoverItem.Value.Stack;
      foreach (ArtisanPriceHelper.ArtisanEntry? entry in artisanEntries)
      {
        if (entry == null)
        {
          continue;
        }

        ArtisanPriceHelper.ArtisanEntry e = entry.Value;
        artisanRowCount++;
        var parts = FormatArtisanPrice(e, hoverStack);
        int w = (int)MeasureArtisanRow(parts);
        artisanMaxTextWidth = Math.Max(artisanMaxTextWidth, w);
      }

      // Walk of Life profession sale bonus (informational - price already includes it)
      string? wolBonusPct = null;
      string? wolBonusLabel = null;
      if (
        itemPrice > 0
        && hoveredObject != null
        && ApiManager.GetApi<IWalkOfLifeApi>(ModCompat.WalkOfLife, out var wolApi)
      )
      {
        float producerBonus = wolApi.GetProducerSaleBonus();
        float anglerBonus = wolApi.GetAnglerSaleBonus();
        if (producerBonus > 1f && IsAnimalProduct(hoveredObject))
        {
          int pct = (int)Math.Round((producerBonus - 1f) * 100f);
          wolBonusPct = $"+{pct}% ";
          wolBonusLabel = "Producer";
        }
        else if (anglerBonus > 1f && IsFishProduct(hoveredObject))
        {
          int pct = (int)Math.Round((anglerBonus - 1f) * 100f);
          wolBonusPct = $"+{pct}% ";
          wolBonusLabel = "Angler";
        }
      }

      bool hasUndonated = showDonation && _donationIcons.HasAnyDonation(_hoverItem.Value);

      bool notShippedYet =
        showShipping
        && hoveredObject != null
        && hoveredObject.countsForShippedCollection()
        && !Game1.player.basicShipped.ContainsKey(hoveredObject.ItemId)
        && hoveredObject.Type != "Fish"
        && hoveredObject.Category != Object.booksCategory
        && hoveredObject.Category != Object.skillBooksCategory;

      string? requiredBundleName = null;
      Color? bundleColor = null;
      int bundleId = -1;
      if (showBundle && hoveredObject != null)
      {
        BundleRequiredItem? bundleDisplayData = BundleHelper.GetBundleItemIfNotDonated(
          hoveredObject
        );
        if (bundleDisplayData != null)
        {
          requiredBundleName = bundleDisplayData.Name;
          bundleId = bundleDisplayData.Id;

          if (!_bundleColorCache.TryGetValue(bundleDisplayData.Id, out bundleColor))
          {
            bundleColor = BundleHelper
              .GetRealColorFromIndex(bundleDisplayData.Id)
              ?.Desaturate(0.35f);
            _bundleColorCache[bundleDisplayData.Id] = bundleColor;
          }
        }
        else
        {
          // Check Unlockable Bundles (lower priority than CC)
          UbBundleRequiredItem? ubData = UnlockableBundleHelper.GetBundleItemIfNotDonated(
            hoveredObject
          );
          if (ubData != null)
          {
            requiredBundleName = ubData.BundleName;
            bundleColor = ParseUbColor(ubData.ColorHex);
            _ubIconOverride = ResolveUbIcon(ubData);
          }
        }
      }

      bool hasPriceRows = itemPrice > 0 || stackPrice > 0 || cropPrice > 0 || artisanRowCount > 0;

      var drawPositionOffset = new Vector2();
      int windowWidth,
        windowHeight;

      var bundleHeaderWidth = 0;
      if (!string.IsNullOrEmpty(requiredBundleName))
      {
        bundleHeaderWidth = 36 + (int)Game1.smallFont.MeasureString(requiredBundleName).X;
      }

      if (hasPriceRows)
      {
        var itemTextWidth = (int)Game1.smallFont.MeasureString(itemPrice.ToString()).X;
        var stackTextWidth = (int)Game1.smallFont.MeasureString(stackPrice.ToString()).X;
        var cropTextWidth = (int)Game1.smallFont.MeasureString(cropPrice.ToString()).X;
        var wolBonusTextWidth =
          wolBonusPct != null
            ? (int)Game1.smallFont.MeasureString(wolBonusPct + wolBonusLabel).X
            : 0;
        var minTextWidth = (int)Game1.smallFont.MeasureString("000").X;
        int largestTextWidth =
          76
          + Math.Max(
            minTextWidth,
            Math.Max(
              artisanMaxTextWidth,
              Math.Max(
                wolBonusTextWidth,
                Math.Max(stackTextWidth, Math.Max(itemTextWidth, cropTextWidth))
              )
            )
          );
        windowWidth = Math.Max(bundleHeaderWidth, largestTextWidth);

        windowHeight = 20 + 16;
        if (itemPrice > 0)
        {
          windowHeight += 40;
        }

        if (stackPrice > 0)
        {
          windowHeight += 40;
        }

        if (cropPrice > 0)
        {
          windowHeight += 40;
        }

        if (wolBonusPct != null)
        {
          windowHeight += 32;
        }

        if (artisanRowCount > 0)
        {
          windowHeight += 40 * artisanRowCount;
        }

        if (!string.IsNullOrEmpty(requiredBundleName))
        {
          windowHeight += 4;
          drawPositionOffset.Y += 4;
        }

        // Min window dimensions
        windowHeight = Math.Max(windowHeight, 40);
        windowWidth = Math.Max(windowWidth, 40);
      }
      else
      {
        // No price box - use bundle header width as reference for standalone elements
        windowWidth = Math.Max(bundleHeaderWidth, 40);
        windowHeight = 0;
      }

      int windowY = Game1.getMouseY() + 20;
      int windowX = Game1.getMouseX() - 25 - windowWidth;

      // Avoid overlapping Ferngill Simple Economy tooltip
      if (
        hoveredObject != null
        && ApiManager.GetApi(ModCompat.FerngillEconomy, out IFerngillSimpleEconomyApi? fseApi)
        && fseApi.IsLoaded()
        && fseApi.ItemIsInEconomy(hoveredObject)
      )
      {
        windowX -= 270;
      }

      // Adjust overflow
      Rectangle safeArea = Utility.getSafeArea();

      if (hasPriceRows && windowY + windowHeight > safeArea.Bottom)
      {
        windowY = safeArea.Bottom - windowHeight;
      }

      if (Game1.getMouseX() + 300 > safeArea.Right)
      {
        windowX = safeArea.Right - 350 - windowWidth;
      }
      else if (windowX < safeArea.Left)
      {
        windowX = Game1.getMouseX() + 350;
      }

      var windowPos = new Vector2(windowX, windowY);

      if (hasPriceRows)
      {
        Vector2 drawPosition = windowPos + new Vector2(16, 20) + drawPositionOffset;

        // 32x40 icon cells, small font cap height 18 offset (2,6)
        var rowHeight = 40;
        var iconCenterOffset = new Vector2(16, 20);
        var textOffset = new Vector2(32 + 4, (rowHeight - 18) / 2 - 6);

        IClickableMenu.drawTextureBox(
          spriteBatch,
          Game1.menuTexture,
          new Rectangle(0, 256, 60, 60),
          (int)windowPos.X,
          (int)windowPos.Y,
          windowWidth,
          windowHeight,
          Color.White
        );

        if (itemPrice > 0)
        {
          spriteBatch.Draw(
            Game1.debrisSpriteSheet,
            drawPosition + iconCenterOffset,
            Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 8, 16, 16),
            Color.White,
            0,
            new Vector2(8, 8),
            Game1.pixelZoom,
            SpriteEffects.None,
            0.95f
          );

          DrawSmallTextWithShadow(spriteBatch, itemPrice.ToString(), drawPosition + textOffset);

          drawPosition.Y += rowHeight;
        }

        if (stackPrice > 0)
        {
          var overlapOffset = new Vector2(0, 10);
          spriteBatch.Draw(
            Game1.debrisSpriteSheet,
            drawPosition + iconCenterOffset - overlapOffset / 2,
            Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 8, 16, 16),
            Color.White,
            0,
            new Vector2(8, 8),
            Game1.pixelZoom,
            SpriteEffects.None,
            0.95f
          );
          spriteBatch.Draw(
            Game1.debrisSpriteSheet,
            drawPosition + iconCenterOffset + overlapOffset / 2,
            Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 8, 16, 16),
            Color.White,
            0,
            new Vector2(8, 8),
            Game1.pixelZoom,
            SpriteEffects.None,
            0.95f
          );

          DrawSmallTextWithShadow(spriteBatch, stackPrice.ToString(), drawPosition + textOffset);

          drawPosition.Y += rowHeight;
        }

        if (cropPrice > 0)
        {
          spriteBatch.Draw(
            Game1.mouseCursors,
            drawPosition + iconCenterOffset,
            new Rectangle(60, 428, 10, 10),
            Color.White,
            0.0f,
            new Vector2(5, 5),
            Game1.pixelZoom * 0.75f,
            SpriteEffects.None,
            0.85f
          );

          DrawSmallTextWithShadow(spriteBatch, cropPrice.ToString(), drawPosition + textOffset);

          drawPosition.Y += rowHeight;
        }

        if (artisanRowCount > 0)
        {
          foreach (ArtisanPriceHelper.ArtisanEntry? entry in artisanEntries)
          {
            if (entry == null)
            {
              continue;
            }

            ArtisanPriceHelper.ArtisanEntry e = entry.Value;
            var parts = FormatArtisanPrice(e, hoverStack);

            DrawArtisanOutputRow(
              spriteBatch,
              e.OutputItem,
              parts,
              drawPosition,
              iconCenterOffset,
              textOffset
            );
            drawPosition.Y += rowHeight;
          }
        }

        if (wolBonusPct != null)
        {
          Vector2 bonusPos = drawPosition + new Vector2(4, 2);
          float pctWidth = Game1.smallFont.MeasureString(wolBonusPct).X;

          // +X% in green
          spriteBatch.DrawString(
            Game1.smallFont,
            wolBonusPct,
            bonusPos + new Vector2(2, 2),
            Game1.textShadowColor
          );
          spriteBatch.DrawString(Game1.smallFont, wolBonusPct, bonusPos, Tools.TooltipGreen);

          // Profession name in normal text color
          Vector2 labelPos = bonusPos + new Vector2(pctWidth, 0);
          spriteBatch.DrawString(
            Game1.smallFont,
            wolBonusLabel!,
            labelPos + new Vector2(2, 2),
            Game1.textShadowColor
          );
          spriteBatch.DrawString(Game1.smallFont, wolBonusLabel!, labelPos, Game1.textColor);
        }
      }

      // For non-price elements, when the price box is hidden we attach to the vanilla tooltip
      Rectangle vanillaTooltip = Rectangle.Empty;
      int informantDecoratorHeight = 0;
      if (!hasPriceRows)
      {
        bool informantSellPrice =
          InformantHelper.IsLoaded && InformantHelper.IsFeatureEnabled("sell-price");
        vanillaTooltip = EstimateVanillaTooltipBounds(_hoverItem.Value, informantSellPrice);

        // Estimate Informant's decorator box height if any known decorators would fire
        if (InformantHelper.IsLoaded)
        {
          bool hasAnyDecorator =
            (hasUndonated && InformantHelper.IsFeatureEnabled("museum"))
            || (notShippedYet && InformantHelper.IsFeatureEnabled("shipping"))
            || (
              !string.IsNullOrEmpty(requiredBundleName)
              && InformantHelper.IsFeatureEnabled("bundles")
            );
          if (hasAnyDecorator)
          {
            // Informant's decorator box: 1 row = Game1.tileSize (64px)
            informantDecoratorHeight = Game1.tileSize;
          }
        }
      }

      if (hasUndonated)
      {
        Vector2 donationPos = hasPriceRows
          ? windowPos + new Vector2(2, windowHeight + 8)
          : new Vector2(vanillaTooltip.Left + 2, vanillaTooltip.Bottom + 8);
        _donationIcons.Draw(spriteBatch, _hoverItem.Value, donationPos);
      }

      if (!string.IsNullOrEmpty(requiredBundleName))
      {
        if (hasPriceRows)
        {
          DrawBundleBanner(
            spriteBatch,
            requiredBundleName,
            bundleId,
            windowPos + new Vector2(-7, -17),
            windowWidth,
            bundleColor,
            _ubIconOverride
          );
        }
        else
        {
          // Shift banner above Informant's decorator box if present
          DrawBundleBanner(
            spriteBatch,
            requiredBundleName,
            bundleId,
            new Vector2(
              vanillaTooltip.Left + 6,
              vanillaTooltip.Top
                - 17
                - informantDecoratorHeight
                + (informantDecoratorHeight > 0 ? 10 : 0)
            ),
            vanillaTooltip.Width,
            bundleColor,
            _ubIconOverride
          );
        }

        _ubIconOverride = null;
      }

      if (notShippedYet)
      {
        bool useBinIcon = config.UseShippingBinIcon;
        if (hasPriceRows)
        {
          Vector2 tabPos = windowPos + new Vector2(windowWidth - 4, 28);
          if (useBinIcon)
          {
            DrawShippingBin(spriteBatch, tabPos + new Vector2(-16, -8), 1.5f);
          }
          else
          {
            DrawCollectionsTab(spriteBatch, tabPos, 2f);
          }
        }
        else
        {
          if (useBinIcon)
          {
            DrawShippingBin(
              spriteBatch,
              new Vector2(
                vanillaTooltip.Right - 24,
                vanillaTooltip.Bottom - 24 - informantDecoratorHeight
              ),
              1.5f
            );
          }
          else
          {
            // Unreversed, attached to the left of the vanilla tooltip
            DrawCollectionsTab(
              spriteBatch,
              new Vector2(
                vanillaTooltip.Left - CollectionsTabSourceRect.Width * 2,
                vanillaTooltip.Top + 28
              ),
              2f,
              flip: false
            );
          }
        }
      }
    }
  }

  /// <summary>
  /// Matches WoL's IsAnimalOrDerivedGood check for Producer bonus display.
  /// Excludes the honey/BeesAreAnimals config edge case.
  /// </summary>
  private static bool IsAnimalProduct(Object obj)
  {
    if (
      obj.Category
      is Object.EggCategory
        or Object.MilkCategory
        or Object.meatCategory
        or Object.sellAtPierresAndMarnies
    )
    {
      return true;
    }

    return obj.QualifiedItemId
      is "(O)107" // Dinosaur Egg
        or "(O)306" // Mayonnaise
        or "(O)307" // Duck Mayonnaise
        or "(O)308" // Void Mayonnaise
        or "(O)807" // Dinosaur Mayonnaise
        or "(O)424" // Cheese
        or "(O)426" // Goat Cheese
        or "(O)428" // Cloth
        or "(O)440" // Wool
        or "(O)DaLion.Professions_GoldenMayo"
        or "(O)DaLion.Professions_OstrichMayo";
  }

  /// <summary>Matches WoL's IsFish + SmokedFish check for Angler bonus display.</summary>
  private static bool IsFishProduct(Object obj)
  {
    return obj.Category == Object.FishCategory
      || obj.preserve?.Value == Object.PreserveType.SmokedFish;
  }

  private void DrawSmallTextWithShadow(
    SpriteBatch b,
    string text,
    Vector2 position,
    Color? color = null,
    float alpha = 1f
  )
  {
    Color shadow = Game1.textShadowColor * alpha;
    Color main = (color ?? Game1.textColor) * alpha;
    b.DrawString(Game1.smallFont, text, position + new Vector2(2, 2), shadow);
    b.DrawString(Game1.smallFont, text, position, main);
  }

  private readonly struct ArtisanRowParts
  {
    public readonly string RatioLabel;
    public readonly string UnitText;
    public readonly string StackPriceLabel;
    public readonly string BatchCountSuffix;

    public ArtisanRowParts(
      string ratioLabel,
      string unitText,
      string stackPriceLabel,
      string batchCountSuffix
    )
    {
      RatioLabel = ratioLabel;
      UnitText = unitText;
      StackPriceLabel = stackPriceLabel;
      BatchCountSuffix = batchCountSuffix;
    }
  }

  private const float ArtisanStackPriceLeadGap = 12f;

  private static ArtisanRowParts FormatArtisanPrice(
    ArtisanPriceHelper.ArtisanEntry e,
    int hoverStack
  )
  {
    int unitMin = e.UnitSellPrice * e.MinOutputStack;
    int unitMax = e.UnitSellPrice * e.MaxOutputStack;
    string unitPart = unitMin == unitMax ? unitMin.ToString() : $"{unitMin}-{unitMax}";

    // "[in:out]" recipe signature - hidden for plain 1:1 recipes to reduce clutter.
    string ratioLabel = "";
    if (e.InputsPerBatch > 1 || e.MaxOutputStack > 1)
    {
      string outPart =
        e.MinOutputStack == e.MaxOutputStack
          ? e.MinOutputStack.ToString()
          : $"{e.MinOutputStack}-{e.MaxOutputStack}";
      ratioLabel = $"[{e.InputsPerBatch}:{outPart}]";
    }

    // Total stack sell price + faded batch count. Skipped at 1 batch (would just duplicate unit).
    int batches = hoverStack / Math.Max(1, e.InputsPerBatch);
    string stackPriceLabel = "";
    string batchCountSuffix = "";
    if (batches >= 2)
    {
      int stackMin = batches * unitMin;
      int stackMax = batches * unitMax;
      stackPriceLabel = stackMin == stackMax ? $"({stackMin})" : $"({stackMin}-{stackMax})";
      batchCountSuffix = $"[{batches}]";
    }

    return new ArtisanRowParts(ratioLabel, unitPart, stackPriceLabel, batchCountSuffix);
  }

  private static float MeasureArtisanRow(ArtisanRowParts parts)
  {
    float width = Game1.smallFont.MeasureString(parts.UnitText).X;
    if (!string.IsNullOrEmpty(parts.RatioLabel))
    {
      width += Game1.smallFont.MeasureString(parts.RatioLabel).X;
    }

    if (!string.IsNullOrEmpty(parts.StackPriceLabel))
    {
      width += ArtisanStackPriceLeadGap + Game1.smallFont.MeasureString(parts.StackPriceLabel).X;
    }

    if (!string.IsNullOrEmpty(parts.BatchCountSuffix))
    {
      width += Game1.smallFont.MeasureString(parts.BatchCountSuffix).X;
    }

    return width;
  }

  private void DrawArtisanOutputRow(
    SpriteBatch spriteBatch,
    Item outputItem,
    ArtisanRowParts parts,
    Vector2 drawPosition,
    Vector2 iconCenterOffset,
    Vector2 textOffset
  )
  {
    // Match the coin's on-screen visual size (~32px) by rendering a 16x16 sprite at scale 2.
    const float scale = 2f;
    Vector2 iconCenter = drawPosition + iconCenterOffset;

    ParsedItemData data = ItemRegistry.GetDataOrErrorItem(outputItem.QualifiedItemId);
    Texture2D tex = data.GetTexture();
    int spriteIndex = outputItem.ParentSheetIndex;
    Rectangle baseRect = data.GetSourceRect(0, spriteIndex);
    var origin = new Vector2(baseRect.Width / 2f, baseRect.Height / 2f);

    if (outputItem is ColoredObject coloredObj)
    {
      if (coloredObj.ColorSameIndexAsParentSheetIndex)
      {
        spriteBatch.Draw(
          tex,
          iconCenter,
          baseRect,
          coloredObj.color.Value,
          0f,
          origin,
          scale,
          SpriteEffects.None,
          0.95f
        );
      }
      else
      {
        Rectangle overlayRect = data.GetSourceRect(1, spriteIndex);
        spriteBatch.Draw(
          tex,
          iconCenter,
          baseRect,
          Color.White,
          0f,
          origin,
          scale,
          SpriteEffects.None,
          0.95f
        );
        spriteBatch.Draw(
          tex,
          iconCenter,
          overlayRect,
          coloredObj.color.Value,
          0f,
          origin,
          scale,
          SpriteEffects.None,
          0.951f
        );
      }
    }
    else
    {
      spriteBatch.Draw(
        tex,
        iconCenter,
        baseRect,
        Color.White,
        0f,
        origin,
        scale,
        SpriteEffects.None,
        0.95f
      );
    }

    Vector2 textPos = drawPosition + textOffset;
    if (!string.IsNullOrEmpty(parts.RatioLabel))
    {
      DrawSmallTextWithShadow(spriteBatch, parts.RatioLabel, textPos, alpha: 0.5f);
      textPos.X += Game1.smallFont.MeasureString(parts.RatioLabel).X;
    }

    DrawSmallTextWithShadow(spriteBatch, parts.UnitText, textPos);

    if (!string.IsNullOrEmpty(parts.StackPriceLabel))
    {
      textPos.X += Game1.smallFont.MeasureString(parts.UnitText).X + ArtisanStackPriceLeadGap;
      DrawSmallTextWithShadow(spriteBatch, parts.StackPriceLabel, textPos);

      if (!string.IsNullOrEmpty(parts.BatchCountSuffix))
      {
        textPos.X += Game1.smallFont.MeasureString(parts.StackPriceLabel).X;
        DrawSmallTextWithShadow(spriteBatch, parts.BatchCountSuffix, textPos, alpha: 0.5f);
      }
    }
  }

  private void DrawBundleBanner(
    SpriteBatch spriteBatch,
    string bundleName,
    int bundleId,
    Vector2 position,
    int windowWidth,
    Color? color = null,
    (Texture2D texture, Rectangle sourceRect)? iconOverride = null
  )
  {
    Color drawColor = color ?? Color.Crimson;

    var bundleBannerX = (int)position.X;
    int bundleBannerY = (int)position.Y + 2;
    var cellCount = 48;
    var solidCells = 10;
    int cellWidth = windowWidth / cellCount;
    for (var cell = 0; cell < cellCount; ++cell)
    {
      float fadeAmount =
        0.97f - (cell < solidCells ? 0 : 1.0f * (cell - solidCells) / (cellCount - solidCells));
      spriteBatch.Draw(
        Game1.staminaRect,
        new Rectangle(bundleBannerX + cell * cellWidth, bundleBannerY, cellWidth, 32),
        drawColor * fadeAmount
      );
    }

    // Draw per-bundle icon at 1:1 pixel scale, fall back to generic scroll if unavailable
    var spriteInfo = iconOverride ?? BundleHelper.GetBundleSpriteInfo(bundleId);
    float iconWidth;
    const int iconDisplaySize = 32;
    if (spriteInfo is var (texture, sourceRect))
    {
      var iconPos = new Point((int)position.X, (int)position.Y + 2);

      // filled rectangle behind the icon acts as a 2px border in bundle color and 1px shadow border
      spriteBatch.Draw(
        Game1.staminaRect,
        new Rectangle(iconPos.X - 2, iconPos.Y - 2, iconDisplaySize + 4, iconDisplaySize + 4),
        drawColor
      );
      spriteBatch.Draw(
        Game1.staminaRect,
        new Rectangle(iconPos.X - 1, iconPos.Y - 1, iconDisplaySize + 2, iconDisplaySize + 2),
        Color.Black * 0.3f
      );
      // incase the icons are smaller then expected triangle fill background with drawColor
      spriteBatch.Draw(
        Game1.staminaRect,
        new Rectangle(iconPos.X, iconPos.Y, iconDisplaySize, iconDisplaySize),
        drawColor
      );

      spriteBatch.Draw(
        texture,
        new Rectangle((int)position.X, (int)position.Y + 2, iconDisplaySize, iconDisplaySize),
        sourceRect,
        Color.White,
        0f,
        Vector2.Zero,
        SpriteEffects.None,
        0.86f
      );

      // Small overlay icon (bottom-right corner) - CC icon or UB book
      Texture2D? overlayTexture = null;
      Rectangle overlayRect;
      int overlayW,
        overlayH;

      if (iconOverride == null)
      {
        // CC bundle - use community center icon from cursors
        overlayW = 13;
        overlayH = 11;
        overlayRect = new Rectangle(332, 375, overlayW, overlayH);
        overlayTexture = Game1.mouseCursors;
      }
      else
      {
        // UB bundle - load book icon from UB's content, stretched to CC overlay size
        try
        {
          overlayTexture = Game1.content.Load<Texture2D>("UnlockableBundles/UI/OverviewBookOpen");
          overlayRect = new Rectangle(0, 0, overlayTexture.Width, overlayTexture.Height);
          overlayW = 13;
          overlayH = 11;
        }
        catch (Exception ex)
        {
          ModEntry.MonitorObject.Log(
            $"ShowItemHoverInformation: failed to load UB overlay texture, {ex.Message}",
            LogLevel.Trace
          );
          overlayW = 0;
          overlayH = 0;
          overlayRect = Rectangle.Empty;
          overlayTexture = null;
        }
      }

      if (overlayTexture != null)
      {
        int overlayX = iconPos.X + iconDisplaySize - overlayW;
        int overlayY = iconPos.Y + iconDisplaySize - overlayH;

        spriteBatch.Draw(
          overlayTexture,
          new Rectangle(overlayX, overlayY, overlayW, overlayH),
          overlayRect,
          Color.White,
          0f,
          Vector2.Zero,
          SpriteEffects.None,
          1f
        );
      }

      iconWidth = iconDisplaySize;
    }
    else
    {
      spriteBatch.Draw(
        Game1.mouseCursors,
        new Rectangle((int)position.X, (int)position.Y + 5, iconDisplaySize, iconDisplaySize),
        _bundleIcon.sourceRect,
        Color.White,
        0f,
        Vector2.Zero,
        SpriteEffects.None,
        0.86f
      );
      iconWidth = iconDisplaySize;
    }

    var textPos = position + new Vector2(iconWidth + 3, 3);
    spriteBatch.DrawString(
      Game1.smallFont,
      bundleName,
      textPos + new Vector2(1, 1),
      Color.Black * 0.3f
    );
    spriteBatch.DrawString(Game1.smallFont, bundleName, textPos, Color.White);
  }

  private static (Texture2D texture, Rectangle sourceRect)? ResolveUbIcon(
    UbBundleRequiredItem ubData
  )
  {
    // Try UB's BundleIconAsset first (complete icon texture)
    if (!string.IsNullOrEmpty(ubData.IconTexturePath))
    {
      try
      {
        Texture2D texture = Game1.content.Load<Texture2D>(ubData.IconTexturePath);
        return (texture, new Rectangle(0, 0, texture.Width, texture.Height));
      }
      catch (Exception ex)
      {
        // BundleIconAsset was set but failed - show error scroll (CP logs the details)
        ModEntry.MonitorObject.Log(
          $"ShowItemHoverInformation: failed to load UB BundleIconAsset '{ubData.IconTexturePath}', {ex.Message}",
          LogLevel.Trace
        );
        return (Game1.mouseCursors, new Rectangle(208, 272, 32, 32));
      }
    }
    else
    {
      ModEntry.MonitorObject.LogOnce(
        $"ShowItemHoverInformation: no BundleIconAsset for UB bundle '{ubData.BundleName}', using fallback",
        LogLevel.Trace
      );
    }

    // No BundleIconAsset set - use UB's book icon, then Cursors scroll as last resort
    try
    {
      Texture2D bookIcon = Game1.content.Load<Texture2D>("UnlockableBundles/UI/BundleOverviewIcon");
      return (bookIcon, new Rectangle(0, 0, bookIcon.Width, bookIcon.Height));
    }
    catch (Exception ex)
    {
      ModEntry.MonitorObject.Log(
        $"ShowItemHoverInformation: failed to load UB book icon, {ex.Message}",
        LogLevel.Trace
      );
      return (Game1.mouseCursors, new Rectangle(208, 272, 32, 32));
    }
  }

  private static readonly Color DefaultUbBundleColor = new(0xDC, 0x7B, 0x05);

  private static Color ParseUbColor(string? hex)
  {
    if (string.IsNullOrEmpty(hex))
    {
      return DefaultUbBundleColor;
    }

    try
    {
      ReadOnlySpan<char> span = hex.AsSpan().TrimStart('#');
      if (span.Length >= 6)
      {
        int r = int.Parse(span[..2], System.Globalization.NumberStyles.HexNumber);
        int g = int.Parse(span[2..4], System.Globalization.NumberStyles.HexNumber);
        int b = int.Parse(span[4..6], System.Globalization.NumberStyles.HexNumber);
        return new Color(r, g, b);
      }
    }
    catch (Exception ex)
    {
      ModEntry.MonitorObject.Log(
        $"ShowItemHoverInformation: invalid UB bundle color hex '{hex}', {ex.Message}",
        LogLevel.Trace
      );
    }

    return DefaultUbBundleColor;
  }

  private static void DrawCollectionsTab(
    SpriteBatch b,
    Vector2 position,
    float scale,
    bool flip = true
  )
  {
    b.Draw(
      Game1.mouseCursors,
      position,
      CollectionsTabSourceRect,
      Color.White,
      0f,
      Vector2.Zero,
      scale,
      flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
      0.86f
    );
  }

  private static void DrawShippingBin(SpriteBatch b, Vector2 position, float scale)
  {
    var shadowOffset = new Vector2(-2, 2);

    // Draw base shadow
    b.Draw(
      Game1.mouseCursors,
      position + shadowOffset,
      ShippingBinBaseRect,
      new Color(0, 0, 0, 50),
      0f,
      Vector2.Zero,
      scale,
      SpriteEffects.None,
      0.859f
    );

    // Draw lid shadow (lower opacity to avoid darkening where it overlaps the base shadow)
    b.Draw(
      Game1.mouseCursors,
      position + shadowOffset,
      ShippingBinLidRect,
      new Color(0, 0, 0, 30),
      0f,
      Vector2.Zero,
      scale,
      SpriteEffects.None,
      0.859f
    );

    // Draw base
    b.Draw(
      Game1.mouseCursors,
      position,
      ShippingBinBaseRect,
      Color.White,
      0f,
      Vector2.Zero,
      scale,
      SpriteEffects.None,
      0.86f
    );

    // Draw lid overlaying the top of the base
    b.Draw(
      Game1.mouseCursors,
      position,
      ShippingBinLidRect,
      Color.White,
      0f,
      Vector2.Zero,
      scale,
      SpriteEffects.None,
      0.861f
    );
  }

  /// <summary>
  /// Estimates the vanilla tooltip box bounds, replicating logic from IClickableMenu.drawHoverText.
  /// </summary>
  private static Rectangle EstimateVanillaTooltipBounds(Item item, bool informantSellPrice = false)
  {
    SpriteFont descFont = Game1.smallFont;
    SpriteFont titleFont = Game1.dialogueFont;

    string description = item.getDescription();
    string title = item.DisplayName;
    string category = item is Object obj ? obj.getCategoryName() : "";

    int width =
      Math.Max((int)descFont.MeasureString(description).X, (int)titleFont.MeasureString(title).X)
      + 32;
    int height =
      (int)descFont.MeasureString(description).Y + 32 + (int)titleFont.MeasureString(title).Y + 16;

    if (category.Length > 0)
    {
      width = Math.Max(width, (int)descFont.MeasureString(category).X + 32);
      height += (int)descFont.MeasureString("T").Y;
    }

    // Food items add edibility stats rows
    if (item is Object edible && edible.Edibility is not (-300 or 0))
    {
      int staminaRecovery = edible.staminaRecoveredOnConsumption();
      int healthRecovery = edible.healthRecoveredOnConsumption();
      height += 40 * ((staminaRecovery > 0 && healthRecovery > 0) ? 2 : 1);
    }

    // Informant's sell-price injects moneyAmountToShowAtBottom into drawToolTip,
    // which the vanilla code adds as: max(font height + 4, 44)
    if (informantSellPrice && item is Object sellable)
    {
      int sellPrice = Utility.getSellToStorePriceOfItem(sellable, false);
      if (sellPrice >= 0 || sellable.canBeShipped())
      {
        height += (int)Math.Max(descFont.MeasureString(sellPrice.ToString()).Y + 4f, 44f);
      }
    }

    height = Math.Max(height, 60);
    width += 4;

    // Position: same as game's drawHoverText
    int x = Game1.getOldMouseX() + 32;
    int y = Game1.getOldMouseY() + 32;

    // Vanilla drawToolTip shifts the tooltip by +40 on both axes when the player is holding
    // an item on the cursor (so it doesn't render under the dragged item sprite).
    if (IsHoldingItemOnCursor())
    {
      x += 40;
      y += 40;
    }

    // Screen edge adjustments matching game logic
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

    return new Rectangle(x, y, width, height);
  }

  private static bool IsHoldingItemOnCursor()
  {
    if (Game1.player.CursorSlotItem != null)
    {
      return true;
    }

    if (Game1.activeClickableMenu is MenuWithInventory mwi && mwi.heldItem != null)
    {
      return true;
    }

    return false;
  }
}
