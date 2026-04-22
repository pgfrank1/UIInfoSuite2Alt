using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using UIInfoSuite2Alt.Infrastructure;

namespace UIInfoSuite2Alt.UIElements;

internal class ShowShopHarvestPrices : IDisposable
{
  private readonly IModHelper _helper;

  public ShowShopHarvestPrices(IModHelper helper)
  {
    _helper = helper;
  }

  public void Dispose()
  {
    ToggleOption(false);
  }

  public void ToggleOption(bool shopHarvestPrices)
  {
    _helper.Events.Display.RenderedActiveMenu -= OnRenderedActiveMenu;

    if (shopHarvestPrices)
    {
      _helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
    }
  }

  private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
  {
    if (!(Game1.activeClickableMenu is ShopMenu menu))
    {
      return;
    }

    if (!(menu.hoveredItem is Item hoverItem))
    {
      return;
    }

    int value = Tools.GetHarvestPrice(hoverItem);

    if (value <= 0)
    {
      return;
    }

    bool gated =
      ModEntry.ModConfig.GatePricesByPriceCatalogue
      && Game1.player.stats.Get(StardewValley.Constants.StatKeys.Book_PriceCatalogue) == 0;

    int xPosition = menu.xPositionOnScreen - 30;
    int yPosition = menu.yPositionOnScreen + 580;
    int boxHeight = gated ? 124 : 108;
    IClickableMenu.drawTextureBox(
      Game1.spriteBatch,
      xPosition + 20,
      yPosition - 52,
      264,
      boxHeight,
      Color.White
    );
    // Title "Harvest Price"
    string textToRender = I18n.HarvestPrice();
    Game1.spriteBatch.DrawString(
      Game1.dialogueFont,
      textToRender,
      new Vector2(xPosition + 30, yPosition - 38),
      Color.Black * 0.2f
    );
    Game1.spriteBatch.DrawString(
      Game1.dialogueFont,
      textToRender,
      new Vector2(xPosition + 32, yPosition - 40),
      Color.Black * 0.8f
    );

    if (gated)
    {
      // Book icon + "Price unknown..." text
      ParsedItemData bookData = ItemRegistry.GetDataOrErrorItem("(O)Book_PriceCatalogue");
      Game1.spriteBatch.Draw(
        bookData.GetTexture(),
        new Vector2(xPosition + 56, yPosition + 28),
        bookData.GetSourceRect(),
        Color.White,
        0,
        new Vector2(8, 8),
        Game1.pixelZoom * 0.75f,
        SpriteEffects.None,
        0.85f
      );
      string unreadText = I18n.PriceCatalogueNotRead();
      var textPos = new Vector2(xPosition + 80, yPosition + 14);
      Game1.spriteBatch.DrawString(
        Game1.smallFont,
        unreadText,
        textPos + new Vector2(2, 2),
        Color.Black * 0.2f
      );
      Game1.spriteBatch.DrawString(Game1.smallFont, unreadText, textPos, Color.Black * 0.8f);
    }
    else
    {
      // Tree Icon
      xPosition += 80;
      Game1.spriteBatch.Draw(
        Game1.mouseCursors,
        new Vector2(xPosition, yPosition),
        new Rectangle(60, 428, 10, 10),
        Color.White,
        0,
        Vector2.Zero,
        Game1.pixelZoom,
        SpriteEffects.None,
        0.85f
      );
      //  Coin
      Game1.spriteBatch.Draw(
        Game1.debrisSpriteSheet,
        new Vector2(xPosition + 32, yPosition + 10),
        Game1.getSourceRectForStandardTileSheet(Game1.debrisSpriteSheet, 8, 16, 16),
        Color.White,
        0,
        new Vector2(8, 8),
        4,
        SpriteEffects.None,
        0.95f
      );
      // Price
      Game1.spriteBatch.DrawString(
        Game1.dialogueFont,
        value.ToString(),
        new Vector2(xPosition + 50, yPosition + 6),
        Color.Black * 0.2f
      );
      Game1.spriteBatch.DrawString(
        Game1.dialogueFont,
        value.ToString(),
        new Vector2(xPosition + 52, yPosition + 4),
        Color.Black * 0.8f
      );
    }
  }
}
