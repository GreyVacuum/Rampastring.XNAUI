using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI.Extensions;
using Rampastring.XNAUI.FontManagement;
using System;
using System.Collections.Generic;

namespace Rampastring.XNAUI.XNAControls;

public enum DropDownState
{
    CLOSED,
    OPENED_DOWN,
    OPENED_UP
}

/// <summary>
/// A drop-down control.
/// </summary>
public class XNADropDown : XNAControl
{
    /// <summary>
    /// Creates a new drop-down control.
    /// </summary>
    /// <param name="windowManager">The WindowManager associated with this control.</param>
    public XNADropDown(WindowManager windowManager) : base(windowManager)
    {
        ItemHeight = UISettings.ActiveSettings.DropDownDefaultItemHeight.GetValueOrDefault((int)FontManager.GetTextDimensions("Test String @", FontIndex).Y + 1);
        Height = ItemHeight + 2;
    }

    public delegate void SelectedIndexChangedEventHandler(object sender, EventArgs e);
    public event SelectedIndexChangedEventHandler SelectedIndexChanged;

    /// <summary>
    /// Raised when the user re-selects an already selected drop-down item.
    /// </summary>
    public event EventHandler IndexReselected;

    /// <summary>
    /// The index of the top-most visible drop down item.
    /// </summary>
    public int TopIndex { get; set; }

    /// <summary>
    /// The height of drop-down items.
    /// </summary>
    public int ItemHeight { get; set; }

    public List<XNADropDownItem> Items = new List<XNADropDownItem>();

    /// <summary>
    /// Gets or sets the dropped-down status of the drop-down control.
    /// </summary>
    public DropDownState DropDownState { get; private set; }

    private bool _allowDropDown = true;

    /// <summary>
    /// Controls whether the drop-down control can be dropped down.
    /// </summary>
    public bool AllowDropDown
    {
        get { return _allowDropDown; }
        set
        {
            _allowDropDown = value;
            if (!_allowDropDown && DropDownState != DropDownState.CLOSED)
            {
                CloseDropDown();
            }
        }
    }

    private int _selectedIndex = -1;

    /// <summary>
    /// Gets or sets the selected index of the drop-down control.
    /// </summary>
    public int SelectedIndex
    {
        get { return _selectedIndex; }
        set
        {
            int oldSelectedIndex = _selectedIndex;

            _selectedIndex = value;

            if (value != oldSelectedIndex)
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            else
                IndexReselected?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets the currently selected item of the drop-down control.
    /// </summary>
    public XNADropDownItem SelectedItem
    {
        get
        {
            if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
                return null;

            return Items[SelectedIndex];
        }
    }

    public int FontIndex { get; set; }

    public bool EnableScrollBar { get; set; } = true;

    public int ScrollBarWidth { get; set; } = 12;

    public Color? ScrollBarThumbColor { get; set; } = null;

    public Color? ScrollBarTrackColor { get; set; } = null;

    public Color? ScrollBarBorderColor { get; set; } = null;

    public int ScrollBarThumbPadding { get; set; } = 2;

    public int ScrollStep { get; set; } = 3;

    public int MaxVisibleItems { get; set; } = 5;

    private bool isScrollBarDragging = false;

    private int scrollBarDragStartY = 0;

    private int scrollBarDragStartTopIndex = 0;

    private Color? _borderColor;

    public Color BorderColor
    {
        get
        {
            return _borderColor ?? UISettings.ActiveSettings.PanelBorderColor;
        }
        set { _borderColor = value; }
    }

    private Color? _focusColor;

    public Color FocusColor
    {
        get
        {
            return _focusColor ?? UISettings.ActiveSettings.FocusColor;
        }
        set { _focusColor = value; }
    }

    private Color? _backColor;

    public Color BackColor
    {
        get
        {
            return _backColor ?? UISettings.ActiveSettings.BackgroundColor;
        }
        set { _backColor = value; }
    }

    private Color? _textColor;

    public Color TextColor
    {
        get
        {
            return _textColor ?? UISettings.ActiveSettings.AltColor;
        }
        set { _textColor = value; }
    }

    private Color? _disabledItemColor;

    public Color DisabledItemColor
    {
        get
        {
            return _disabledItemColor ?? UISettings.ActiveSettings.DisabledItemColor;
        }
        set { _disabledItemColor = value; }
    }

    /// <summary>
    /// If set, the drop-down is opened upwards rather than downwards.
    /// </summary>
    public bool OpenUp { get; set; }

    private bool _showEllipsisOnOverflow = false;

    /// <summary>
    /// When the selected item's text is too wide for the closed control, it is always
    /// cut off at the last fully fitting character so it does not visually overrun the
    /// control's bounds. If this property is enabled, an ellipsis ("...") is appended
    /// to indicate that the text has been truncated.
    /// </summary>
    public bool ShowEllipsisOnOverflow
    {
        get => _showEllipsisOnOverflow;
        set
        {
            _showEllipsisOnOverflow = value;
            InvalidateDisplayTextCache();
        }
    }

    public Texture2D DropDownTexture { get; set; }
    public Texture2D DropDownOpenTexture { get; set; }

    public EnhancedSoundEffect ClickSoundEffect { get; set; }

    private int hoveredIndex = 0;
    private bool clickedAfterOpen = false;
    private int numFittingItems = 0;

    /// <summary>
    /// The width of the open dropdown list. Computed in <see cref="OpenDropDown"/> as the
    /// maximum of <see cref="XNAControl.Width"/> and the widest item's content width, so
    /// wide items remain fully visible when the list is expanded.
    /// </summary>
    private int expandedListWidth = 0;

    /// <summary>
    /// Stores the control width used when the dropdown is closed so it can be restored
    /// after the expanded list has temporarily widened the control. Null when no closed
    /// width has been captured yet, so a legitimate <see cref="XNAControl.Width"/> of 0
    /// is preserved instead of being treated as "not captured".
    /// </summary>
    private int? closedWidth = null;

    /// <summary>
    /// Ensures the framework-visible control width matches the actual visible dropdown
    /// width while the list is open, and restores the original width when the list closes.
    /// </summary>
    private void SyncWidthToDropDownState()
    {
        if (DropDownState != DropDownState.CLOSED)
        {
            if (closedWidth == null)
                closedWidth = Width;

            int openWidth = Math.Max(closedWidth.Value, expandedListWidth);
            if (Width != openWidth)
                Width = openWidth;
        }
        else
        {
            if (closedWidth != null && Width != closedWidth.Value)
                Width = closedWidth.Value;
            closedWidth = null;
            expandedListWidth = 0;
        }
    }

    /// <summary>
    /// Current visible width of the control, accounting for whether the dropdown list
    /// is open and may be wider than the closed-state <see cref="XNAControl.Width"/>.
    /// </summary>
    private int CurrentDisplayWidth
    {
        get
        {
            SyncWidthToDropDownState();
            return Width;
        }
    }

    // Cache for the truncated display text produced by GetDisplayTextForSelectedItem.
    // That method is called every frame from DrawSelectedItem; without this cache it
    // would re-run the MeasureString truncation on each draw.
    private (string cachedDisplayText, int cachedSelectedIndex, int cachedWidth, int cachedFontIndex, string cachedItemText) displayTextCache = (null, -1, 0, 0, null);

    #region AddItem methods

    /// <summary>
    /// Adds an item into the drop-down.
    /// </summary>
    /// <param name="item">The item.</param>
    public void AddItem(XNADropDownItem item)
    {
        Items.Add(item);
    }

    /// <summary>
    /// Generates and adds an item with the specified text into the drop-down.
    /// </summary>
    /// <param name="text">The text of the item.</param>
    public void AddItem(string text)
    {
        var item = new XNADropDownItem();
        item.Text = text;

        Items.Add(item);
    }

    /// <summary>
    /// Generates and adds an item with the specified text and texture
    /// into the drop-down.
    /// </summary>
    /// <param name="text">The text of the item.</param>
    /// <param name="texture">The item's texture.</param>
    public void AddItem(string text, Texture2D texture)
    {
        var item = new XNADropDownItem();
        item.Text = text;
        item.Texture = texture;

        Items.Add(item);
    }

    /// <summary>
    /// Generates and adds an item with the specified text
    /// and text color into the drop-down control.
    /// </summary>
    /// <param name="text">The text of the item.</param>
    /// <param name="color">The color of the item's text.</param>
    public void AddItem(string text, Color color)
    {
        var item = new XNADropDownItem();
        item.Text = text;
        item.TextColor = color;

        Items.Add(item);
    }

    #endregion

    public override void Initialize()
    {
        base.Initialize();

        DropDownTexture = AssetLoader.LoadTexture("comboBoxArrow.png");
        DropDownOpenTexture = AssetLoader.LoadTexture("openedComboBoxArrow.png");

        Height = DropDownTexture.Height;

        ClientRectangleUpdated += (s, e) => InvalidateDisplayTextCache();
    }

    protected override void ParseControlINIAttribute(IniFile iniFile, string key, string value)
    {
        switch (key)
        {
            case "OpenUp":
                OpenUp = Conversions.BooleanFromString(value, OpenUp);
                return;
            case "DropDownTexture":
                DropDownTexture = AssetLoader.LoadTextureUncached(value);
                return;
            case "DropDownOpenTexture":
                DropDownOpenTexture = AssetLoader.LoadTextureUncached(value);
                return;
            case "ItemHeight":
                ItemHeight = Conversions.IntFromString(value, ItemHeight);
                return;
            case "ClickSoundEffect":
                ClickSoundEffect = new EnhancedSoundEffect(value);
                return;
            case "FontIndex":
                FontIndex = Conversions.IntFromString(value, FontIndex);
                return;
            case "BorderColor":
                BorderColor = AssetLoader.GetRGBAColorFromString(value);
                return;
            case "FocusColor":
                FocusColor = AssetLoader.GetRGBAColorFromString(value);
                return;
            case "BackColor":
                BackColor = AssetLoader.GetRGBAColorFromString(value);
                return;
            case "DisabledItemColor":
                DisabledItemColor = AssetLoader.GetColorFromString(value);
                return;
            case "ShowEllipsisOnOverflow":
                ShowEllipsisOnOverflow = Conversions.BooleanFromString(value, false);
                return;
            case "EnableScrollBar":
                EnableScrollBar = Conversions.BooleanFromString(value, true);
                return;
            case "ScrollBarWidth":
                ScrollBarWidth = Conversions.IntFromString(value, 12);
                return;
            case "ScrollBarThumbColor":
                ScrollBarThumbColor = AssetLoader.GetRGBAColorFromString(value);
                return;
            case "ScrollBarTrackColor":
                ScrollBarTrackColor = AssetLoader.GetRGBAColorFromString(value);
                return;
            case "ScrollBarBorderColor":
                ScrollBarBorderColor = AssetLoader.GetRGBAColorFromString(value);
                return;
            case "ScrollBarThumbPadding":
                ScrollBarThumbPadding = Conversions.IntFromString(value, 2);
                return;
            case "ScrollStep":
                ScrollStep = Conversions.IntFromString(value, 3);
                return;
            case "MaxVisibleItems":
                MaxVisibleItems = Conversions.IntFromString(value, 5);
                return;
        }

        if (key.StartsWith("Option", StringComparison.InvariantCulture))
        {
            AddItem(value);
            return;
        }

        base.ParseControlINIAttribute(iniFile, key, value);
    }

    /// <summary>
    /// Gets the text color of a drop-down item.
    /// </summary>
    /// <param name="item">The item.</param>
    protected Color GetItemTextColor(XNADropDownItem item) =>
        item.TextColor ?? TextColor;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (DropDownState != DropDownState.CLOSED)
        {
            if (isScrollBarDragging)
            {
                if (Cursor.RightDown)
                {
                    UpdateScrollBarDrag();
                }
                else
                {
                    isScrollBarDragging = false;
                }
                return;
            }

            if (!IsActive && Cursor.LeftPressedDown)
            {
                hoveredIndex = -1;
                CloseDropDown();
                return;
            }

            int itemIndexOnCursor = GetItemIndexOnCursor();

            if (itemIndexOnCursor > -1 && Items[itemIndexOnCursor].Selectable)
                hoveredIndex = itemIndexOnCursor;
            else
                hoveredIndex = -1;
        }
    }

    public override void OnRightClick(InputEventArgs inputEventArgs)
    {
        base.OnRightClick(inputEventArgs);

        if (DropDownState != DropDownState.CLOSED && EnableScrollBar && Items.Count > numFittingItems)
        {
            Rectangle listRectangle;
            if (DropDownState == DropDownState.OPENED_DOWN)
                listRectangle = new Rectangle(0, DropDownTexture.Height, CurrentDisplayWidth, Height - DropDownTexture.Height);
            else
                listRectangle = new Rectangle(0, 0, CurrentDisplayWidth, Height - DropDownTexture.Height);

            Rectangle scrollBarRect = GetScrollBarTrackRectangle(listRectangle);
            Point cursorPos = GetCursorPoint();

            if (scrollBarRect.Contains(cursorPos))
            {
                inputEventArgs.Handled = true;

                Rectangle thumbRect = GetScrollBarThumbRectangle(listRectangle);

                if (thumbRect.Contains(cursorPos))
                {
                    isScrollBarDragging = true;
                    scrollBarDragStartY = cursorPos.Y;
                    scrollBarDragStartTopIndex = TopIndex;
                }
                else
                {
                    int scrollBarY = scrollBarRect.Y;
                    int scrollBarHeight = scrollBarRect.Height;
                    int totalItems = Items.Count;
                    int visibleItems = Math.Min(numFittingItems, totalItems);
                    float clickRatio = (float)(cursorPos.Y - scrollBarY) / scrollBarHeight;
                    int newTopIndex = (int)(clickRatio * (totalItems - visibleItems));
                    TopIndex = (int)MathHelper.Clamp(newTopIndex, 0, totalItems - visibleItems);
                }
            }
        }
    }

    public override void OnMouseLeftDown(InputEventArgs inputEventArgs)
    {
        base.OnMouseLeftDown(inputEventArgs);

        if (!AllowDropDown)
            return;

        inputEventArgs.Handled = true;

        if (DropDownState != DropDownState.CLOSED)
        {
            if (EnableScrollBar && Items.Count > numFittingItems)
            {
                Rectangle listRectangle;
                if (DropDownState == DropDownState.OPENED_DOWN)
                    listRectangle = new Rectangle(0, DropDownTexture.Height, CurrentDisplayWidth, Height - DropDownTexture.Height);
                else
                    listRectangle = new Rectangle(0, 0, CurrentDisplayWidth, Height - DropDownTexture.Height);

                Rectangle scrollBarRect = GetScrollBarTrackRectangle(listRectangle);
                Point cursorPos = GetCursorPoint();

                if (scrollBarRect.Contains(cursorPos))
                {
                    Rectangle thumbRect = GetScrollBarThumbRectangle(listRectangle);

                    if (thumbRect.Contains(cursorPos))
                    {
                        // Left-click on thumb: jump to that position (no drag)
                        float scrollBarHeight = scrollBarRect.Height;
                        int totalItems = Items.Count;
                        int visibleItems = Math.Min(numFittingItems, totalItems);
                        int thumbHeight = thumbRect.Height;
                        float clickRatio = (float)(cursorPos.Y - scrollBarRect.Y - thumbHeight / 2f) / (scrollBarHeight - thumbHeight);
                        int newTopIndex = (int)(clickRatio * (totalItems - visibleItems));
                        TopIndex = (int)MathHelper.Clamp(newTopIndex, 0, totalItems - visibleItems);
                    }
                    else
                    {
                        int scrollBarY = scrollBarRect.Y;
                        int scrollBarHeight = scrollBarRect.Height;
                        int totalItems = Items.Count;
                        int visibleItems = Math.Min(numFittingItems, totalItems);
                        float clickRatio = (float)(cursorPos.Y - scrollBarY) / scrollBarHeight;
                        int newTopIndex = (int)(clickRatio * (totalItems - visibleItems));
                        TopIndex = (int)MathHelper.Clamp(newTopIndex, 0, totalItems - visibleItems);
                    }
                    return;
                }
            }
            return;
        }

        ClickSoundEffect?.Play();

        clickedAfterOpen = false;

        OpenDropDown();

        Detach();
        hoveredIndex = -1;
    }

    public virtual void OpenDropDown()
    {
        TopIndex = 0;

        // What's the max width that fits all items
        expandedListWidth = Width;
        foreach (var item in Items)
        {
            int itemWidth = 4; //padding
            if (item.Texture != null)
                itemWidth += item.Texture.Width + 1;
            if (item.Text != null)
                itemWidth += (int)Math.Ceiling(Renderer.MeasureString(item.Text, FontIndex).X);

            if (itemWidth > expandedListWidth)
                expandedListWidth = itemWidth;
        }

        if (!OpenUp)
        {
            DropDownState = DropDownState.OPENED_DOWN;
            numFittingItems = (WindowManager.RenderResolutionY - (GetWindowRectangle().Bottom + 1)) / (ItemHeight * GetTotalScalingRecursive());
            numFittingItems = Math.Max(1, numFittingItems);
            if (MaxVisibleItems > 0)
                numFittingItems = Math.Min(numFittingItems, MaxVisibleItems);
            Height = DropDownTexture.Height + 2 + ItemHeight * Math.Min(numFittingItems, Items.Count);
        }
        else
        {
            DropDownState = DropDownState.OPENED_UP;
            int availableSpace = GetWindowRectangle().Y;
            numFittingItems = availableSpace / (ItemHeight * GetTotalScalingRecursive());
            numFittingItems = Math.Max(1, numFittingItems);
            if (MaxVisibleItems > 0)
                numFittingItems = Math.Min(numFittingItems, MaxVisibleItems);
            int itemsToShow = Math.Min(numFittingItems, Items.Count);
            Y -= 1 + ItemHeight * itemsToShow;
            Height = DropDownTexture.Height + 1 + ItemHeight * itemsToShow;
        }

        SyncWidthToDropDownState();
    }

    public override void OnLeftClick(InputEventArgs inputEventArgs)
    {
        base.OnLeftClick(inputEventArgs);
        inputEventArgs.Handled = true;

        if (DropDownState == DropDownState.CLOSED)
        {
            return;
        }

        if (isScrollBarDragging)
        {
            return;
        }

        int itemIndexOnCursor = GetItemIndexOnCursor();

        if (itemIndexOnCursor == -1 && !clickedAfterOpen)
        {
            clickedAfterOpen = true;
            return;
        }

        if (itemIndexOnCursor > -1)
        {
            if (Items[itemIndexOnCursor].Selectable)
                SelectedIndex = itemIndexOnCursor;
            else
                return;
        }

        ClickSoundEffect?.Play();

        CloseDropDown();
    }

    protected virtual void CloseDropDown()
    {
        if (DropDownState == DropDownState.OPENED_UP)
        {
            Y = Bottom - DropDownTexture.Height;
        }

        Height = DropDownTexture.Height;
        DropDownState = DropDownState.CLOSED;
        isScrollBarDragging = false;
        SyncWidthToDropDownState();
        Attach();
    }

    public override void OnMouseScrolled(InputEventArgs inputEventArgs)
    {
        if (!AllowDropDown)
            return;

        bool cursorOnButton;
        if (DropDownState == DropDownState.CLOSED)
            cursorOnButton = true;
        else if (DropDownState == DropDownState.OPENED_DOWN)
            cursorOnButton = GetCursorPoint().Y <= DropDownTexture.Height;
        else // OPENED_UP
            cursorOnButton = GetCursorPoint().Y >= Height - DropDownTexture.Height;

        if (cursorOnButton)
        {
            if (Cursor.ScrollWheelValue < 0)
            {
                if (SelectedIndex >= Items.Count - 1)
                    return;

                inputEventArgs.Handled = true;

                if (Items[SelectedIndex + 1].Selectable)
                    SelectedIndex++;
            }

            if (Cursor.ScrollWheelValue > 0)
            {
                if (SelectedIndex < 1)
                    return;

                inputEventArgs.Handled = true;

                if (Items[SelectedIndex - 1].Selectable)
                    SelectedIndex--;
            }
        }
        else if (AllowScrollingItemList())
        {
            if (Cursor.ScrollWheelValue < 0)
            {
                if (TopIndex + numFittingItems < Items.Count)
                {
                    inputEventArgs.Handled = true;
                    TopIndex = Math.Min(Items.Count - numFittingItems, TopIndex + ScrollStep);
                }
            }
            else if (Cursor.ScrollWheelValue > 0)
            {
                if (TopIndex > 0)
                {
                    inputEventArgs.Handled = true;
                    TopIndex = Math.Max(0, TopIndex - ScrollStep);
                }
            }
        }

        base.OnMouseScrolled(inputEventArgs);
    }

    private bool AllowScrollingItemList()
    {
        return numFittingItems < Items.Count;
    }

    /// <summary>
    /// Returns the index of the item that the cursor currently points to.
    /// </summary>
    private int GetItemIndexOnCursor()
    {
        Point p = GetCursorPoint();

        if (p.X < 0 || p.X > CurrentDisplayWidth ||
            p.Y > Height ||
            p.Y < 0)
        {
            return -2;
        }

        if (DropDownState != DropDownState.CLOSED && EnableScrollBar && Items.Count > numFittingItems)
        {
            if (p.X > CurrentDisplayWidth - ScrollBarWidth)
                return -1;
        }

        int itemIndex;

        if (DropDownState == DropDownState.OPENED_DOWN)
        {
            if (p.Y < DropDownTexture.Height + 1)
                return -1;

            int y = p.Y - DropDownTexture.Height - 1;
            itemIndex = TopIndex + (y / ItemHeight);
        }
        else // if (DropDownState == DropDownState.OPENED_UP)
        {
            if (p.Y > ClientRectangle.Height - DropDownTexture.Height - 1)
                return -1;

            itemIndex = (p.Y - 1) / ItemHeight;
        }

        if (itemIndex < Items.Count && itemIndex > -1)
        {
            return itemIndex;
        }

        return -1;
    }

    /// <summary>
    /// Invalidates the cached display text.
    /// </summary>
    private void InvalidateDisplayTextCache()
    {
        displayTextCache = (null, -1, 0, 0, null);
    }

    /// <summary>
    /// Gets the display text for the selected item, fitted to the control's bounds so
    /// it does not visually overrun the control. Appends an ellipsis to indicate
    /// truncation when <see cref="ShowEllipsisOnOverflow"/> is enabled.
    /// </summary>
    private string GetDisplayTextForSelectedItem(XNADropDownItem item, int textX)
    {
        (string cachedDisplayText, int cachedSelectedIndex, int cachedWidth, int cachedFontIndex, string cachedItemText) = displayTextCache;

        if (cachedDisplayText != null &&
            cachedSelectedIndex == SelectedIndex &&
            cachedWidth == Width &&
            cachedFontIndex == FontIndex &&
            cachedItemText == item.Text)
        {
            return cachedDisplayText;
        }

        string displayText = item.Text;

        if (item.Text != null)
        {
            int availableWidth = ShowEllipsisOnOverflow ? (Width - textX - DropDownTexture.Width - 2) : (Width - textX - 2);

            if (availableWidth <= 0)
            {
                displayText = string.Empty;
            }
            else
            {
                Vector2 textSize = Renderer.MeasureString(item.Text, FontIndex);

                if (textSize.X > availableWidth)
                {
                    const string ellipsis = "...";
                    float ellipsisWidth = ShowEllipsisOnOverflow
                        ? Renderer.MeasureString(ellipsis, FontIndex).X
                        : 0f;
                    float maxWidth = availableWidth - ellipsisWidth;

                    if (maxWidth <= 0)
                    {
                        displayText = string.Empty;
                    }
                    else
                    {
                        int bestFit = 0;
                        int low = 0;
                        int high = item.Text.Length;

                        while (low <= high)
                        {
                            int mid = low + ((high - low) / 2);
                            string test = item.Text.SubstringSurrogateAware(0, mid);
                            float currentWidth = Renderer.MeasureString(test, FontIndex).X;

                            if (currentWidth <= maxWidth)
                            {
                                bestFit = mid;
                                low = mid + 1;
                            }
                            else
                            {
                                high = mid - 1;
                            }
                        }

                        displayText = item.Text.SubstringSurrogateAware(0, bestFit);
                        if (ShowEllipsisOnOverflow)
                            displayText += ellipsis;
                    }
                }
            }
        }

        displayTextCache = (displayText, SelectedIndex, Width, FontIndex, item.Text);

        return displayText;
    }

    /// <summary>
    /// Draws the drop-down.
    /// </summary>
    public override void Draw(GameTime gameTime)
    {
        Rectangle dropDownRect;
        if (DropDownState == DropDownState.CLOSED)
            dropDownRect = new Rectangle(0, 0, Width, Height);
        else if (DropDownState == DropDownState.OPENED_DOWN)
            dropDownRect = new Rectangle(0, 0, Width, DropDownTexture.Height);
        else
            dropDownRect = new Rectangle(0, Height - DropDownTexture.Height, Width, DropDownTexture.Height);

        FillRectangle(new Rectangle(dropDownRect.X + 1, dropDownRect.Y + 1,
            dropDownRect.Width - 2, dropDownRect.Height - 2), BackColor);
        DrawRectangle(dropDownRect, BorderColor);

        if (SelectedIndex > -1 && SelectedIndex < Items.Count)
        {
            XNADropDownItem item = Items[SelectedIndex];

            int textX = 3;
            if (item.Texture != null)
            {
                DrawTexture(item.Texture,
                    new Rectangle(1, dropDownRect.Y + 2,
                    item.Texture.Width, item.Texture.Height), Color.White);
                textX += item.Texture.Width + 1;
            }

            if (item.Text != null)
            {
                string displayText = GetDisplayTextForSelectedItem(item, textX);
                int textY = dropDownRect.Y + Renderer.GetTextYPadding(displayText, FontIndex, dropDownRect.Height);
                DrawStringWithShadow(displayText, FontIndex,
                    new Vector2(textX, textY), GetItemTextColor(item));
            }
        }

        if (AllowDropDown)
        {
            var ddRectangle = new Rectangle(Width - DropDownTexture.Width,
                dropDownRect.Y, DropDownTexture.Width, DropDownTexture.Height);

            if (DropDownState != DropDownState.CLOSED)
            {
                DrawTexture(DropDownOpenTexture,
                    ddRectangle, RemapColor);

                Rectangle listRectangle;

                if (DropDownState == DropDownState.OPENED_DOWN)
                    listRectangle = new Rectangle(0, DropDownTexture.Height, CurrentDisplayWidth, Height - DropDownTexture.Height);
                else
                    listRectangle = new Rectangle(0, 0, CurrentDisplayWidth, Height - DropDownTexture.Height);

                DrawRectangle(listRectangle, BorderColor);

                bool showScrollBar = EnableScrollBar && Items.Count > numFittingItems;
                int itemAreaWidth = showScrollBar ? CurrentDisplayWidth - ScrollBarWidth - 1 : CurrentDisplayWidth;

                for (int i = 0; i < Math.Min(numFittingItems, Items.Count - TopIndex); i++)
                {
                    int y = listRectangle.Y + 1 + i * ItemHeight;
                    DrawItem(TopIndex + i, y, itemAreaWidth);
                }

                if (showScrollBar)
                {
                    DrawScrollBar(listRectangle);
                }
            }
            else
            {
                DrawTexture(DropDownTexture, ddRectangle, RemapColor);
            }
        }

        base.Draw(gameTime);
    }

    /// <summary>
    /// Draws a single drop-down item.
    /// This can be overridden in derived classes to customize the drawing code.
    /// </summary>
    /// <param name="index">The index of the item to be drawn.</param>
    /// <param name="y">The Y coordinate of the item's top border.</param>
    protected virtual void DrawItem(int index, int y)
    {
        DrawItem(index, y, CurrentDisplayWidth);
    }

    /// <summary>
    /// Draws a single drop-down item with a specified width.
    /// </summary>
    /// <param name="index">The index of the item to be drawn.</param>
    /// <param name="y">The Y coordinate of the item's top border.</param>
    /// <param name="itemWidth">The width of the item area.</param>
    protected virtual void DrawItem(int index, int y, int itemWidth)
    {
        XNADropDownItem item = Items[index];

        if (hoveredIndex == index)
        {
            FillRectangle(new Rectangle(1, y, itemWidth - 2, ItemHeight), FocusColor);
        }
        else
        {
            FillRectangle(new Rectangle(1, y, itemWidth - 2, ItemHeight), BackColor);
        }

        int textX = 2;
        if (item.Texture != null)
        {
            DrawTexture(item.Texture, new Rectangle(1, y + 1, item.Texture.Width, item.Texture.Height), Color.White);
            textX += item.Texture.Width + 1;
        }

        Color textColor;

        if (item.Selectable)
            textColor = GetItemTextColor(item);
        else
            textColor = DisabledItemColor;

        if (item.Text != null)
        {
            int textY = y + Renderer.GetTextYPadding(item.Text, FontIndex, ItemHeight);
            DrawStringWithShadow(item.Text, FontIndex, new Vector2(textX, textY), textColor);
        }
    }

    private void DrawScrollBar(Rectangle listRectangle)
    {
        int scrollBarX = listRectangle.Right - ScrollBarWidth;
        int scrollBarY = listRectangle.Y + 1;
        int scrollBarHeight = listRectangle.Height - 2;
        int totalItems = Items.Count;
        int visibleItems = Math.Min(numFittingItems, totalItems);
        float scrollRatio = (float)visibleItems / totalItems;
        int thumbHeight = Math.Max(ItemHeight, (int)(scrollBarHeight * scrollRatio));
        float scrollProgress = (float)TopIndex / (totalItems - visibleItems);
        int thumbY = scrollBarY + (int)((scrollBarHeight - thumbHeight) * scrollProgress);

        Color trackColor = ScrollBarTrackColor ?? UISettings.ActiveSettings.DropDownScrollBarTrackColor ?? BackColor;
        Color borderColor = ScrollBarBorderColor ?? UISettings.ActiveSettings.DropDownScrollBarBorderColor ?? BorderColor;
        Color thumbColor = isScrollBarDragging ? FocusColor : (ScrollBarThumbColor ?? UISettings.ActiveSettings.DropDownScrollBarThumbColor ?? UISettings.ActiveSettings.AltColor);

        FillRectangle(new Rectangle(scrollBarX, scrollBarY, ScrollBarWidth, scrollBarHeight), trackColor);
        DrawRectangle(new Rectangle(scrollBarX, scrollBarY, ScrollBarWidth, scrollBarHeight), borderColor);

        FillRectangle(new Rectangle(scrollBarX + ScrollBarThumbPadding, thumbY,
            ScrollBarWidth - ScrollBarThumbPadding * 2, thumbHeight), thumbColor);
    }

    private Rectangle GetScrollBarThumbRectangle(Rectangle listRectangle)
    {
        int scrollBarX = listRectangle.Right - ScrollBarWidth;
        int scrollBarY = listRectangle.Y + 1;
        int scrollBarHeight = listRectangle.Height - 2;
        int totalItems = Items.Count;
        int visibleItems = Math.Min(numFittingItems, totalItems);
        float scrollRatio = (float)visibleItems / totalItems;
        int thumbHeight = Math.Max(ItemHeight, (int)(scrollBarHeight * scrollRatio));
        float scrollProgress = (float)TopIndex / (totalItems - visibleItems);
        int thumbY = scrollBarY + (int)((scrollBarHeight - thumbHeight) * scrollProgress);

        return new Rectangle(scrollBarX, thumbY, ScrollBarWidth, thumbHeight);
    }

    private Rectangle GetScrollBarTrackRectangle(Rectangle listRectangle)
    {
        int scrollBarX = listRectangle.Right - ScrollBarWidth;
        int scrollBarY = listRectangle.Y + 1;
        int scrollBarHeight = listRectangle.Height - 2;

        return new Rectangle(scrollBarX, scrollBarY, ScrollBarWidth, scrollBarHeight);
    }

    private void UpdateScrollBarDrag()
    {
        Rectangle listRectangle;
        if (DropDownState == DropDownState.OPENED_DOWN)
            listRectangle = new Rectangle(0, DropDownTexture.Height, CurrentDisplayWidth, Height - DropDownTexture.Height);
        else
            listRectangle = new Rectangle(0, 0, CurrentDisplayWidth, Height - DropDownTexture.Height);

        int scrollBarY = listRectangle.Y + 1;
        int scrollBarHeight = listRectangle.Height - 2;
        int totalItems = Items.Count;
        int visibleItems = Math.Min(numFittingItems, totalItems);
        int thumbHeight = Math.Max(ItemHeight, (int)(scrollBarHeight * ((float)visibleItems / totalItems)));

        Point cursorPos = GetCursorPoint();
        int deltaY = cursorPos.Y - scrollBarDragStartY;
        float scrollDelta = (float)deltaY / (scrollBarHeight - thumbHeight);
        int newTopIndex = scrollBarDragStartTopIndex + (int)(scrollDelta * (totalItems - visibleItems));
        TopIndex = (int)MathHelper.Clamp(newTopIndex, 0, totalItems - visibleItems);
    }
}
