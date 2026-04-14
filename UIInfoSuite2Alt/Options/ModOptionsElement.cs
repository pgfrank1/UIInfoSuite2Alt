using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;

namespace UIInfoSuite2Alt.Options;

public class ModOptionsElement
{
  protected const int DefaultX = 8;
  protected const int DefaultY = 4;
  protected const int DefaultPixelSize = 9;

  protected readonly string _label;

  protected readonly ModOptionsElement? _parent;
  private readonly int _whichOption;
  private readonly bool _isSubtitle;
  private readonly bool _isSmallText;
  private readonly bool _isCentered;
  private readonly Color? _textColor;
  private readonly bool _isVertCentered;

  public ModOptionsElement(
    string label,
    int whichOption = -1,
    ModOptionsElement? parent = null,
    bool isSubtitle = false,
    bool isSmallText = false,
    bool isCentered = false,
    Color? textColor = null,
    bool isVertCentered = false,
    bool isIndented = false
  )
  {
    int x = DefaultX * Game1.pixelZoom;
    int y = DefaultY * Game1.pixelZoom;
    int width = DefaultPixelSize * Game1.pixelZoom;
    int height = DefaultPixelSize * Game1.pixelZoom;

    if (parent != null || isIndented)
    {
      x += DefaultX * 2 * Game1.pixelZoom;
    }

    if (isSmallText)
    {
      y -= Game1.pixelZoom * 3;
    }

    Bounds = new Rectangle(x, y, width, height);
    _label = label;
    _whichOption = whichOption;

    _parent = parent;
    _isSubtitle = isSubtitle;
    _isSmallText = isSmallText;
    _isCentered = isCentered;
    _textColor = textColor;
    _isVertCentered = isVertCentered;
  }

  public Rectangle Bounds { get; protected set; }

  public virtual void ReceiveLeftClick(int x, int y) { }

  public virtual void LeftClickHeld(int x, int y) { }

  public virtual void LeftClickReleased(int x, int y) { }

  public virtual void ReceiveKeyPress(Keys key) { }

  public virtual void Draw(SpriteBatch batch, int slotX, int slotY)
  {
    if (_isSmallText)
    {
      float drawX = slotX + Bounds.X;
      if (_isCentered)
      {
        float textWidth = Game1.smallFont.MeasureString(_label).X;
        int slotWidth = Game1.activeClickableMenu?.width ?? Game1.uiViewport.Width;
        drawX = slotX + (slotWidth - Game1.tileSize / 2 - textWidth) / 2f;
      }

      float drawY = slotY + Bounds.Y;
      if (_isVertCentered && Game1.activeClickableMenu != null)
      {
        int slotHeight =
          (Game1.activeClickableMenu.height - Game1.tileSize * 2) / 7 + Game1.pixelZoom;
        float textHeight = Game1.smallFont.MeasureString(_label).Y;
        drawY = slotY + (slotHeight - textHeight) / 2f;
      }

      Utility.drawTextWithShadow(
        batch,
        _label,
        Game1.smallFont,
        new Vector2(drawX, drawY),
        _textColor ?? Game1.textColor,
        1f,
        0.1f
      );
    }
    else if (_isSubtitle)
    {
      Utility.drawTextWithShadow(
        batch,
        _label,
        Game1.dialogueFont,
        new Vector2(slotX + Bounds.X, slotY + Bounds.Y),
        Game1.textColor,
        1f,
        0.1f
      );
    }
    else if (_whichOption < 0)
    {
      int drawX = slotX + Bounds.X;
      if (_isCentered)
      {
        int textWidth = SpriteText.getWidthOfString(_label);
        int slotWidth = Game1.activeClickableMenu?.width ?? Game1.uiViewport.Width;
        drawX = slotX + (slotWidth - Game1.tileSize / 2 - textWidth) / 2;
      }

      int drawY = slotY + Bounds.Y;
      if (_isVertCentered && Game1.activeClickableMenu != null)
      {
        int slotHeight =
          (Game1.activeClickableMenu.height - Game1.tileSize * 2) / 7 + Game1.pixelZoom;
        int textHeight = SpriteText.getHeightOfString(_label);
        drawY = slotY + (slotHeight - textHeight) / 2 + Game1.pixelZoom * 3;
      }

      SpriteText.drawString(batch, _label, drawX, drawY, 999, -1, 999, 1, 0.1f);
    }
    else
    {
      Utility.drawTextWithShadow(
        batch,
        _label,
        Game1.dialogueFont,
        new Vector2(slotX + Bounds.X + Bounds.Width + Game1.pixelZoom * 2, slotY + Bounds.Y),
        Game1.textColor,
        1f,
        0.1f
      );
    }
  }

  public virtual Point? GetRelativeSnapPoint(Rectangle slotBounds)
  {
    // Positioning taken from OptionsPage.snapCursorToCurrentSnappedComponent
    return new Point(48, slotBounds.Height / 2 - 12);
  }
}
