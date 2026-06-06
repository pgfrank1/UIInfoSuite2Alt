using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace UIInfoSuite2Alt.Options;

/// <summary>Our mod options made page to be added to <see cref="GameMenu.pages" /></summary>
public class ModOptionsPage : IClickableMenu, IDisposable
{
  
  private readonly ClickableTextureComponent _downArrow;
  private readonly IModEvents _events;
  private readonly List<ModOptionsElement> _options;
  private readonly ClickableTextureComponent _scrollBar;
  private readonly ClickableTextureComponent _upArrow;
  private int _currentItemIndex;
  private string _hoverText = "";
  private bool _isScrolling;

  /// <summary>Visible option slots. Public so populateClickableComponentList can find it.</summary>
  public List<ClickableComponent> _optionSlots = new();

  private int _optionsSlotHeld = -1;
  private Rectangle _scrollBarRunner;

  public ModOptionsPage(List<ModOptionsElement> options, IModEvents events)
    : this(options, events, Game1.activeClickableMenu) { }

  public ModOptionsPage(
    List<ModOptionsElement> options,
    IModEvents events,
    IClickableMenu parentMenu
  )
    : base(
      parentMenu.xPositionOnScreen,
      parentMenu.yPositionOnScreen + 10,
      parentMenu.width,
      parentMenu.height
    )
  {
    _options = options;
    _events = events;
    _upArrow = new ClickableTextureComponent(
      new Rectangle(
        xPositionOnScreen + width + Game1.tileSize / 4,
        yPositionOnScreen + Game1.tileSize,
        11 * Game1.pixelZoom,
        12 * Game1.pixelZoom
      ),
      Game1.mouseCursors,
      new Rectangle(421, 459, 11, 12),
      Game1.pixelZoom
    );

    _downArrow = new ClickableTextureComponent(
      new Rectangle(
        _upArrow.bounds.X,
        yPositionOnScreen + height - Game1.tileSize,
        _upArrow.bounds.Width,
        _upArrow.bounds.Height
      ),
      Game1.mouseCursors,
      new Rectangle(421, 472, 11, 12),
      Game1.pixelZoom
    );

    _scrollBar = new ClickableTextureComponent(
      new Rectangle(
        _upArrow.bounds.X + Game1.pixelZoom * 3,
        _upArrow.bounds.Y + _upArrow.bounds.Height + Game1.pixelZoom,
        6 * Game1.pixelZoom,
        10 * Game1.pixelZoom
      ),
      Game1.mouseCursors,
      new Rectangle(435, 463, 6, 10),
      Game1.pixelZoom
    );

    _scrollBarRunner = new Rectangle(
      _scrollBar.bounds.X,
      _scrollBar.bounds.Y,
      _scrollBar.bounds.Width,
      height - Game1.tileSize * 2 - _upArrow.bounds.Height - Game1.pixelZoom * 2
    );

    LayoutSlots();

    events.Display.MenuChanged += OnMenuChanged;
  }

  /// <summary>Raised after a game menu is opened, closed, or replaced.</summary>
  /// <param name="sender">The event sender.</param>
  /// <param name="e">The event arguments.</param>
  private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
  {
    if (e.NewMenu is GameMenu)
    {
      xPositionOnScreen = e.NewMenu.xPositionOnScreen;
      yPositionOnScreen = e.NewMenu.yPositionOnScreen + 10;
      height = e.NewMenu.height;

      _upArrow.bounds.X = xPositionOnScreen + width + Game1.tileSize / 4;
      _upArrow.bounds.Y = yPositionOnScreen + Game1.tileSize;
      _upArrow.bounds.Width = 11 * Game1.pixelZoom;
      _upArrow.bounds.Height = 12 * Game1.pixelZoom;

      _downArrow.bounds.X = _upArrow.bounds.X;
      _downArrow.bounds.Y = yPositionOnScreen + height - Game1.tileSize;
      _downArrow.bounds.Width = _upArrow.bounds.Width;
      _downArrow.bounds.Height = _upArrow.bounds.Height;

      _scrollBar.bounds.X = _upArrow.bounds.X + Game1.pixelZoom * 3;
      _scrollBar.bounds.Y = _upArrow.bounds.Y + _upArrow.bounds.Height + Game1.pixelZoom;
      _scrollBar.bounds.Width = 6 * Game1.pixelZoom;
      _scrollBar.bounds.Height = 10 * Game1.pixelZoom;

      _scrollBarRunner.X = _scrollBar.bounds.X;
      _scrollBarRunner.Y = _scrollBar.bounds.Y;
      _scrollBarRunner.Height =
        height - Game1.tileSize * 2 - _upArrow.bounds.Height - Game1.pixelZoom * 2;

      LayoutSlots();
    }
  }

  public override void snapToDefaultClickableComponent()
  {
    int firstInteractive = -1;
    for (int i = 0; i < _optionSlots.Count; i++)
    {
      if (_options[_currentItemIndex + i].IsInteractive)
      {
        firstInteractive = i;
        break;
      }
    }
    currentlySnappedComponent = firstInteractive != -1 ? getComponentWithID(firstInteractive) : null;
    snapCursorToCurrentSnappedComponent();
  }

  protected override void customSnapBehavior(int direction, int oldRegion, int oldID)
  {
    if (direction == Game1.down)
    {
      int lastInteractiveID = -1;
      for (int i = _optionSlots.Count - 1; i >= 0; i--)
      {
        if (_options[_currentItemIndex + i].IsInteractive)
        {
          lastInteractiveID = i;
          break;
        }
      }

      if (oldID == lastInteractiveID)
      {
        if (_currentItemIndex < GetMaxScrollIndex())
        {
          int targetOptionIndex = _currentItemIndex + oldID + 1;
          while (targetOptionIndex < _options.Count && !_options[targetOptionIndex].IsInteractive)
          {
            targetOptionIndex++;
          }

          if (targetOptionIndex < _options.Count)
          {
            int bottomY = yPositionOnScreen + height - Game1.tileSize;
            while (true)
            {
              int slotIndex = targetOptionIndex - _currentItemIndex;
              if (slotIndex >= 0 && slotIndex < _optionSlots.Count)
              {
                if (_optionSlots[slotIndex].bounds.Bottom > bottomY && _currentItemIndex < GetMaxScrollIndex())
                {
                  _currentItemIndex++;
                  LayoutSlots();
                }
                else
                {
                  break;
                }
              }
              else if (slotIndex >= _optionSlots.Count && _currentItemIndex < GetMaxScrollIndex())
              {
                _currentItemIndex++;
                LayoutSlots();
              }
              else
              {
                break;
              }
            }

            SetScrollBarToCurrentItem();
            int newSlotIndex = targetOptionIndex - _currentItemIndex;
            if (newSlotIndex >= 0 && newSlotIndex < _optionSlots.Count)
            {
              currentlySnappedComponent = _optionSlots[newSlotIndex];
              snapCursorToCurrentSnappedComponent();
              Game1.playSound("shiny4");
            }
          }
        }
      }
    }
    else if (direction == Game1.up)
    {
      int firstInteractiveID = -1;
      for (int i = 0; i < _optionSlots.Count; i++)
      {
        if (_options[_currentItemIndex + i].IsInteractive)
        {
          firstInteractiveID = i;
          break;
        }
      }

      if (oldID == firstInteractiveID)
      {
        if (_currentItemIndex > 0)
        {
          int targetOptionIndex = _currentItemIndex + oldID - 1;
          while (targetOptionIndex >= 0 && !_options[targetOptionIndex].IsInteractive)
          {
            targetOptionIndex--;
          }

          if (targetOptionIndex >= 0)
          {
            int topY = yPositionOnScreen + Game1.tileSize * 5 / 4 + Game1.pixelZoom;
            while (true)
            {
              int slotIndex = targetOptionIndex - _currentItemIndex;
              if (slotIndex >= 0 && slotIndex < _optionSlots.Count)
              {
                if (_optionSlots[slotIndex].bounds.Top < topY && _currentItemIndex > 0)
                {
                  _currentItemIndex--;
                  LayoutSlots();
                }
                else
                {
                  break;
                }
              }
              else if (slotIndex < 0 && _currentItemIndex > 0)
              {
                _currentItemIndex--;
                LayoutSlots();
              }
              else
              {
                break;
              }
            }

            SetScrollBarToCurrentItem();
            int newSlotIndex = targetOptionIndex - _currentItemIndex;
            if (newSlotIndex >= 0 && newSlotIndex < _optionSlots.Count)
            {
              currentlySnappedComponent = _optionSlots[newSlotIndex];
              snapCursorToCurrentSnappedComponent();
              Game1.playSound("shiny4");
            }
          }
        }
        else
        {
          // Already at the top, move to the menu tab
          currentlySnappedComponent = getComponentWithID(ModOptionsPageHandler.ModTabSnapId);
          if (currentlySnappedComponent != null)
          {
            // Set the down neighbor of the tab to the first slot, instead of the default (which is the second slot)
            currentlySnappedComponent.downNeighborID = 0;
          }

          snapCursorToCurrentSnappedComponent();
        }
      }
    }
  }

  public override void snapCursorToCurrentSnappedComponent()
  {
    if (currentlySnappedComponent != null && currentlySnappedComponent.myID < _optionSlots.Count)
    {
      int targetOptionIndex = _currentItemIndex + currentlySnappedComponent.myID;
      int bottomY = yPositionOnScreen + height - Game1.tileSize;
      int topY = yPositionOnScreen + Game1.tileSize * 5 / 4 + Game1.pixelZoom;

      bool scrolled = false;
      while (true)
      {
        int slotIndex = targetOptionIndex - _currentItemIndex;
        if (slotIndex >= 0 && slotIndex < _optionSlots.Count)
        {
          ClickableComponent slot = _optionSlots[slotIndex];
          if (slot.bounds.Bottom > bottomY && _currentItemIndex < GetMaxScrollIndex())
          {
            _currentItemIndex++;
            LayoutSlots();
            scrolled = true;
          }
          else if (slot.bounds.Top < topY && _currentItemIndex > 0)
          {
            _currentItemIndex--;
            LayoutSlots();
            scrolled = true;
          }
          else
          {
            break;
          }
        }
        else
        {
          break;
        }
      }

      if (scrolled)
      {
        SetScrollBarToCurrentItem();
        int newSlotIndex = targetOptionIndex - _currentItemIndex;
        if (newSlotIndex >= 0 && newSlotIndex < _optionSlots.Count)
        {
          currentlySnappedComponent = _optionSlots[newSlotIndex];
        }
      }

      ModOptionsElement? snappedElement = GetVisibleOption(currentlySnappedComponent.myID);
      if (snappedElement != null)
      {
        Point? maybePos = snappedElement.GetRelativeSnapPoint(currentlySnappedComponent.bounds);
        if (maybePos is Point pos) // if it's not null
        {
          Game1.setMousePosition(
            currentlySnappedComponent.bounds.X + pos.X,
            currentlySnappedComponent.bounds.Y + pos.Y
          );
          return;
        }
      }

      Game1.setMousePosition(
        currentlySnappedComponent.bounds.Left + 48,
        currentlySnappedComponent.bounds.Center.Y - 12
      );
    }
    else
    {
      base.snapCursorToCurrentSnappedComponent();
    }
  }

  private void SetScrollBarToCurrentItem()
  {
    if (_options.Count > 0)
    {
      int maxScroll = GetMaxScrollIndex();
      _scrollBar.bounds.Y =
        _scrollBarRunner.Height / Math.Max(1, maxScroll + 1) * _currentItemIndex
        + _upArrow.bounds.Bottom
        + Game1.pixelZoom;

      if (_currentItemIndex == maxScroll)
      {
        _scrollBar.bounds.Y = _downArrow.bounds.Y - _scrollBar.bounds.Height - Game1.pixelZoom;
      }
    }
  }

  public override void leftClickHeld(int x, int y)
  {
    if (!GameMenu.forcePreventClose)
    {
      base.leftClickHeld(x, y);

      if (_isScrolling)
      {
        int yBefore = _scrollBar.bounds.Y;

        _scrollBar.bounds.Y = Math.Min(
          yPositionOnScreen
            + height
            - Game1.tileSize
            - Game1.pixelZoom * 3
            - _scrollBar.bounds.Height,
          Math.Max(y, yPositionOnScreen + _upArrow.bounds.Height + Game1.pixelZoom * 5)
        );

        _currentItemIndex = Math.Max(
          0,
          Math.Min(
            GetMaxScrollIndex(),
            _options.Count * (y - _scrollBarRunner.Y) / _scrollBarRunner.Height
          )
        );

        SetScrollBarToCurrentItem();
        LayoutSlots();

        if (yBefore != _scrollBar.bounds.Y)
        {
          Game1.playSound("shiny4");
        }
      }
      else if (_optionsSlotHeld > -1 && _optionsSlotHeld + _currentItemIndex < _options.Count)
      {
        _options[_currentItemIndex + _optionsSlotHeld]
          .LeftClickHeld(
            x - _optionSlots[_optionsSlotHeld].bounds.X,
            y - _optionSlots[_optionsSlotHeld].bounds.Y
          );
      }
    }
  }

  public override void receiveKeyPress(Keys key)
  {
    if (_optionsSlotHeld > -1 && _optionsSlotHeld + _currentItemIndex < _options.Count)
    {
      _options[_currentItemIndex + _optionsSlotHeld].ReceiveKeyPress(key);
    }
    else
    {
      // Gamepad left/right: forward to snapped element (e.g. number picker arrows)
      if (
        Game1.options.snappyMenus
        && Game1.options.gamepadControls
        && currentlySnappedComponent != null
        && (
          Game1.options.doesInputListContain(Game1.options.moveLeftButton, key)
          || Game1.options.doesInputListContain(Game1.options.moveRightButton, key)
        )
      )
      {
        int index = _currentItemIndex + currentlySnappedComponent.myID;
        if (index >= 0 && index < _options.Count)
        {
          _options[index].ReceiveKeyPress(key);
        }
      }

      // The base implementation handles gamepad movement
      base.receiveKeyPress(key);
    }
  }

  public override void receiveScrollWheelAction(int direction)
  {
    if (!GameMenu.forcePreventClose)
    {
      base.receiveScrollWheelAction(direction);

      if (direction > 0 && _currentItemIndex > 0)
      {
        UpArrowPressed();
        Game1.playSound("shiny4");
      }
      else if (direction < 0 && _currentItemIndex < GetMaxScrollIndex())
      {
        DownArrowPressed();
        Game1.playSound("shiny4");
      }
    }
  }

  public override void releaseLeftClick(int x, int y)
  {
    if (!GameMenu.forcePreventClose)
    {
      base.releaseLeftClick(x, y);

      if (_optionsSlotHeld > -1 && _optionsSlotHeld + _currentItemIndex < _options.Count)
      {
        ClickableComponent optionSlot = _optionSlots[_optionsSlotHeld];
        _options[_currentItemIndex + _optionsSlotHeld]
          .LeftClickReleased(x - optionSlot.bounds.X, y - optionSlot.bounds.Y);
      }

      _optionsSlotHeld = -1;
      _isScrolling = false;
    }
  }

  private void DownArrowPressed()
  {
    _downArrow.scale = _downArrow.baseScale;
    ++_currentItemIndex;
    SetScrollBarToCurrentItem();
    LayoutSlots();
  }

  private void UpArrowPressed()
  {
    _upArrow.scale = _upArrow.baseScale;
    --_currentItemIndex;
    SetScrollBarToCurrentItem();
    LayoutSlots();
  }

  public override void receiveLeftClick(int x, int y, bool playSound = true)
  {
    if (!GameMenu.forcePreventClose)
    {
      if (_downArrow.containsPoint(x, y) && _currentItemIndex < GetMaxScrollIndex())
      {
        DownArrowPressed();
        Game1.playSound("shwip");
      }
      else if (_upArrow.containsPoint(x, y) && _currentItemIndex > 0)
      {
        UpArrowPressed();
        Game1.playSound("shwip");
      }
      else if (_scrollBar.containsPoint(x, y))
      {
        _isScrolling = true;
      }
      else if (
        !_downArrow.containsPoint(x, y)
        && x > xPositionOnScreen + width
        && x < xPositionOnScreen + width + Game1.tileSize * 2
        && y > yPositionOnScreen
        && y < yPositionOnScreen + height
      )
      {
        // Handle scrollbar click even if the player clicked right next to it, but do not enable scrollbar dragging
        // NB the leniency area is based on the option page's, so it's too large
        _isScrolling = true;
        base.leftClickHeld(x, y);
        base.releaseLeftClick(x, y);
      }

      _currentItemIndex = Math.Max(0, Math.Min(GetMaxScrollIndex(), _currentItemIndex));
      for (var i = 0; i < _optionSlots.Count; ++i)
      {
        if (
          _optionSlots[i].bounds.Contains(x, y)
          && _currentItemIndex + i < _options.Count
          && _options[_currentItemIndex + i]
            .Bounds.Contains(x - _optionSlots[i].bounds.X, y - _optionSlots[i].bounds.Y)
        )
        {
          _options[_currentItemIndex + i]
            .ReceiveLeftClick(x - _optionSlots[i].bounds.X, y - _optionSlots[i].bounds.Y);
          _optionsSlotHeld = i;
          break;
        }
      }
    }
  }

  public override void receiveRightClick(int x, int y, bool playSound = true) { }

  public override void performHoverAction(int x, int y)
  {
    if (!GameMenu.forcePreventClose)
    {
      _hoverText = "";
      _upArrow.tryHover(x, y);
      _downArrow.tryHover(x, y);
      _scrollBar.tryHover(x, y);
    }
  }

  public override void draw(SpriteBatch batch)
  {
    Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen - 10, width, height, false, true);
    batch.End();

    RasterizerState scissorState = new RasterizerState { ScissorTestEnable = true };
    batch.Begin(SpriteSortMode.FrontToBack, BlendState.NonPremultiplied, SamplerState.PointClamp, null, scissorState);

    Rectangle prevScissor = batch.GraphicsDevice.ScissorRectangle;
    Rectangle newScissor = new Rectangle(
        xPositionOnScreen, 
        yPositionOnScreen + Game1.tileSize, 
        width, 
        height - Game1.tileSize * 2
    );

    // Ensure scissor rectangle stays within screen bounds to avoid MonoGame crashes
    if (newScissor.X < 0) newScissor.X = 0;
    if (newScissor.Y < 0) newScissor.Y = 0;
    if (newScissor.Right > batch.GraphicsDevice.Viewport.Width) newScissor.Width = batch.GraphicsDevice.Viewport.Width - newScissor.X;
    if (newScissor.Bottom > batch.GraphicsDevice.Viewport.Height) newScissor.Height = batch.GraphicsDevice.Viewport.Height - newScissor.Y;

    batch.GraphicsDevice.ScissorRectangle = newScissor;

    for (var i = 0; i < _optionSlots.Count; ++i)
    {
      if (_currentItemIndex >= 0 && _currentItemIndex + i < _options.Count)
      {
        _options[_currentItemIndex + i]
          .Draw(batch, _optionSlots[i].bounds.X, _optionSlots[i].bounds.Y);
      }
    }

    batch.End();
    batch.GraphicsDevice.ScissorRectangle = prevScissor;

    batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
    if (!GameMenu.forcePreventClose)
    {
      _upArrow.draw(batch);
      _downArrow.draw(batch);
      if (_options.Count > 7)
      {
        drawTextureBox(
          batch,
          Game1.mouseCursors,
          new Rectangle(403, 383, 6, 6),
          _scrollBarRunner.X,
          _scrollBarRunner.Y,
          _scrollBarRunner.Width,
          _scrollBarRunner.Height,
          Color.White,
          Game1.pixelZoom,
          false
        );
        _scrollBar.draw(batch);
      }
    }

    if (_hoverText != "")
    {
      drawHoverText(batch, _hoverText, Game1.smallFont);
    }
  }

  /// <summary>Returns the <see cref="ModOptionsElement" /> that corresponds to the component ID</summary>
  /// <returns>the mod options element, or null if it is invalid</returns>
  private int GetMaxScrollIndex()
  {
    int topY = yPositionOnScreen + Game1.tileSize * 5 / 4 + Game1.pixelZoom;
    int bottomY = yPositionOnScreen + height - Game1.tileSize;
    int availableHeight = bottomY - topY;

    int currentHeight = 0;
    for (int i = _options.Count - 1; i >= 0; i--)
    {
      currentHeight += _options[i].Height;
      if (currentHeight > availableHeight)
      {
        return Math.Min(_options.Count - 1, i + 1);
      }
    }
    return 0;
  }

  private ModOptionsElement? GetVisibleOption(int componentId)
  {
    if (componentId >= _optionSlots.Count)
    {
      return null;
    }

    int index = _currentItemIndex + componentId;
    if (0 <= index && index < _options.Count)
    {
      return _options[index];
    }

    return null;
  }

  private void LayoutSlots()
  {
    int topY = yPositionOnScreen + Game1.tileSize * 5 / 4 + Game1.pixelZoom;
    int bottomY = yPositionOnScreen + height - Game1.tileSize;
    int currentY = topY;

    if (_currentItemIndex >= GetMaxScrollIndex())
    {
      int totalHeight = 0;
      for (int i = _currentItemIndex; i < _options.Count; i++)
      {
         totalHeight += _options[i].Height;
      }
      int availableHeight = bottomY - topY;
      if (totalHeight > availableHeight)
      {
         currentY = bottomY - totalHeight;
      }
    }

    int slotIndex = 0;
    for (int i = 0; _currentItemIndex + i < _options.Count; i++)
    {
      int itemHeight = _options[_currentItemIndex + i].Height;

      if (slotIndex >= _optionSlots.Count)
      {
        var component = new ClickableComponent(
          new Rectangle(
            xPositionOnScreen + Game1.tileSize / 4,
            currentY,
            width - Game1.tileSize / 2,
            itemHeight
          ),
          slotIndex.ToString()
        )
        {
          myID = slotIndex,
          fullyImmutable = true,
        };
        _optionSlots.Add(component);
        allClickableComponents?.Add(component);
      }
      else
      {
        _optionSlots[slotIndex].bounds.Y = currentY;
        _optionSlots[slotIndex].bounds.Height = itemHeight;
      }

      currentY += itemHeight;
      slotIndex++;

      if (currentY >= bottomY)
      {
        break; // Stop adding once we exceeded the visible area
      }
    }

    while (_optionSlots.Count > slotIndex)
    {
      allClickableComponents?.Remove(_optionSlots[_optionSlots.Count - 1]);
      _optionSlots.RemoveAt(_optionSlots.Count - 1);
    }

    for (int i = 0; i < _optionSlots.Count; i++)
    {
      int nextInteractive = -1;
      for (int j = _currentItemIndex + i + 1; j < _options.Count; j++)
      {
        if (_options[j].IsInteractive)
        {
          int slotJ = j - _currentItemIndex;
          nextInteractive = slotJ < _optionSlots.Count
            ? slotJ
            : ClickableComponent.CUSTOM_SNAP_BEHAVIOR;
          break;
        }
      }
  
      int prevInteractive = -1;
      for (int j = _currentItemIndex + i - 1; j >= 0; j--)
      {
        if (_options[j].IsInteractive)
        {
          int slotJ = j - _currentItemIndex;
          prevInteractive = slotJ >= 0
            ? slotJ
            : ClickableComponent.CUSTOM_SNAP_BEHAVIOR;
          break;
        }
      }
  
      _optionSlots[i].downNeighborID = nextInteractive != -1 ? nextInteractive : ClickableComponent.CUSTOM_SNAP_BEHAVIOR;
      _optionSlots[i].upNeighborID = prevInteractive != -1 ? prevInteractive : ClickableComponent.CUSTOM_SNAP_BEHAVIOR;
    }

    if (currentlySnappedComponent != null && currentlySnappedComponent.myID >= _optionSlots.Count)
    {
      // If we shrank the slots and the cursor is out of bounds, snap it to the last interactive slot
      for (int i = _optionSlots.Count - 1; i >= 0; i--)
      {
        if (_options[_currentItemIndex + i].IsInteractive)
        {
          currentlySnappedComponent = _optionSlots[i];
          break;
        }
      }
    }
  }

  internal void SaveState(ModOptionsPageState state)
  {
    state.currentIndex = _currentItemIndex;
    state.currentComponent = currentlySnappedComponent?.myID;
  }

  /// <summary>Clamp scroll position after the options list has been modified externally.</summary>
  internal void ClampScrollPosition()
  {
    _currentItemIndex = Math.Min(_currentItemIndex, GetMaxScrollIndex());
    _currentItemIndex = Math.Max(0, _currentItemIndex);

    SetScrollBarToCurrentItem();
    LayoutSlots();
  }

  internal void LoadState(ModOptionsPageState state)
  {
    if (state.currentIndex is int index)
    {
      _currentItemIndex = index;
    }

    if (state.currentComponent is int componentID)
    {
      ClickableComponent? component = getComponentWithID(componentID);
      if (component != null)
      {
        currentlySnappedComponent = component;
        snapCursorToCurrentSnappedComponent();
      }
    }
  }

  public void Dispose()
  {
    _events.Display.MenuChanged -= OnMenuChanged;
  }
}

