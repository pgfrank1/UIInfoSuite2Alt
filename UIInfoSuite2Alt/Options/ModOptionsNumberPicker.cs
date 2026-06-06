using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace UIInfoSuite2Alt.Options;

/// <summary>Number picker element with left/right arrows for the mod options page.</summary>
internal class ModOptionsNumberPicker : ModOptionsElement
{
  private static readonly Rectangle LeftArrowSource = new(352, 495, 12, 11);
  private static readonly Rectangle RightArrowSource = new(365, 495, 12, 11);

  private readonly Action<int> _setOption;
  private readonly int _minValue;
  private readonly int _maxValue;
  private int _value;

  private readonly Rectangle _leftArrowBounds;
  private readonly Rectangle _valueBounds;
  private readonly Rectangle _rightArrowBounds;

  public ModOptionsNumberPicker(
    string label,
    int whichOption,
    Func<int> getOption,
    Action<int> setOption,
    int minValue = 1,
    int maxValue = 20
  )
    : base(label, whichOption)
  {
    _setOption = setOption;
    _minValue = minValue;
    _maxValue = maxValue;
    _value = Math.Clamp(getOption(), minValue, maxValue);

    int scale = Game1.pixelZoom;
    int arrowW = 12 * scale;
    int arrowH = 11 * scale;
    int numberW = 12 * scale;

    // Height is calculated from slotHeight which is usually 68. (68 - 44) / 2 = 12.
    int y = Bounds.Y + Game1.pixelZoom * 3;
    _leftArrowBounds = new Rectangle(Bounds.X, y, arrowW, arrowH);
    _valueBounds = new Rectangle(Bounds.X + arrowW + 4, y, numberW, arrowH);
    _rightArrowBounds = new Rectangle(Bounds.X + arrowW + 4 + numberW + 4, y, arrowW, arrowH);

    // Expand Bounds so the gate check in ModOptionsPage covers all sub-elements
    Bounds = new Rectangle(Bounds.X, y, _rightArrowBounds.Right - Bounds.X, arrowH);
  }

  public override void ReceiveLeftClick(int x, int y)
  {
    if (_leftArrowBounds.Contains(x, y))
    {
      _value = _value <= _minValue ? _maxValue : _value - 1;
      _setOption(_value);
      Game1.playSound("smallSelect");
    }
    else if (_rightArrowBounds.Contains(x, y))
    {
      _value = _value >= _maxValue ? _minValue : _value + 1;
      _setOption(_value);
      Game1.playSound("smallSelect");
    }
  }

  public override void ReceiveKeyPress(Keys key)
  {
    if (!Game1.options.SnappyMenus)
      return;

    if (Game1.options.doesInputListContain(Game1.options.moveRightButton, key))
    {
      _value = _value >= _maxValue ? _minValue : _value + 1;
      _setOption(_value);
      Game1.playSound("smallSelect");
    }
    else if (Game1.options.doesInputListContain(Game1.options.moveLeftButton, key))
    {
      _value = _value <= _minValue ? _maxValue : _value - 1;
      _setOption(_value);
      Game1.playSound("smallSelect");
    }
  }

  public override void Draw(SpriteBatch batch, int slotX, int slotY)
  {
    // Left arrow
    batch.Draw(
      Game1.mouseCursors,
      new Vector2(slotX + _leftArrowBounds.X, slotY + _leftArrowBounds.Y),
      LeftArrowSource,
      Color.White,
      0f,
      Vector2.Zero,
      Game1.pixelZoom,
      SpriteEffects.None,
      0.4f
    );

    // Number value (centered in value area)
    string text = _value.ToString();
    Vector2 textSize = Game1.dialogueFont.MeasureString(text);
    Utility.drawTextWithShadow(
      batch,
      text,
      Game1.dialogueFont,
      new Vector2(
        slotX + _valueBounds.X + (_valueBounds.Width - textSize.X) / 2f,
        slotY + _valueBounds.Y - 4
      ),
      Game1.textColor,
      1f,
      0.1f
    );

    // Right arrow
    batch.Draw(
      Game1.mouseCursors,
      new Vector2(slotX + _rightArrowBounds.X, slotY + _rightArrowBounds.Y),
      RightArrowSource,
      Color.White,
      0f,
      Vector2.Zero,
      Game1.pixelZoom,
      SpriteEffects.None,
      0.4f
    );

    // Label text after the arrows
    Utility.drawTextWithShadow(
      batch,
      _label,
      Game1.dialogueFont,
      new Vector2(slotX + _rightArrowBounds.Right + Game1.pixelZoom * 2, slotY + Bounds.Y),
      Game1.textColor,
      1f,
      0.1f
    );
  }

  public override Point? GetRelativeSnapPoint(Rectangle slotBounds)
  {
    return new Point(Bounds.X + 16, Bounds.Y + 13);
  }
}
