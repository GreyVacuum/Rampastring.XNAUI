using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rampastring.XNAUI.XNAControls;

public class TabEventArgs : EventArgs
{
    public TabEventArgs(Tab tab)
    {
        Tab = tab;
    }

    public Tab Tab { get; }
}

/// <summary>
/// A control that has multiple tabs, of which only one can be selected at a time.
/// </summary>
public class XNATabControl : XNAControl
{
    public XNATabControl(WindowManager windowManager) : base(windowManager)
    {
    }

    private void UpdateLayout()
    {
        Width = 0;
        Height = 0;

        foreach (var t in Tabs)
        {
            if (t.DefaultTexture == null)
                continue;

            if (TabDirection == TabDirectionType.Horizontal)
            {
                Width += t.DefaultTexture.Width;
                Height = t.DefaultTexture.Height;
            }
            else
            {
                Width = Math.Max(Width, t.DefaultTexture.Width);
                Height += t.DefaultTexture.Height;
            }
        }
    }

    public event EventHandler<TabEventArgs> SelectedTabChanged;
    public event EventHandler SelectedIndexChanged;
    public event EventHandler<TabEventArgs> HoveredTabChanged;

    private int _selectedTabIndex = -1;
    private int _selectedIndexChangeDepth;

    private void FireSelectedIndexChangedEvent()
    {
        _selectedIndexChangeDepth++;
        try
        {
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _selectedIndexChangeDepth--;
        }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (value >= Tabs.Count || value < -1)
                value = -1; // Normalize "not selected" as -1

            if (value == _selectedTabIndex)
                return;

            Tab previousTab = SelectedTab;
            _selectedTabIndex = value;

            // Properly handle cases where the handler of a tab-selection event changes the tab.
            // Only the "outermost" tab change emits SelectedTabChanged.
            FireSelectedIndexChangedEvent();

            if (_selectedIndexChangeDepth != 0)
                return;

            Tab newTab = SelectedTab;
            if (previousTab != newTab)
                SelectedTabChanged?.Invoke(this, new TabEventArgs(newTab));
        }
    }

    public Tab SelectedTab
    {
        get
        {
            if (_selectedTabIndex < 0 || _selectedTabIndex >= Tabs.Count)
                return null;

            return Tabs[_selectedTabIndex];
        }
        set
        {
            if (value == null)
            {
                SelectedTabIndex = -1;
                return;
            }

            int tabIndex = Tabs.IndexOf(value);
            if (tabIndex < 0)
                throw new InvalidOperationException("Attempted to select a tab that is not part of this TabControl.");

            if (tabIndex == SelectedTabIndex)
                return;

            SelectedTabIndex = tabIndex;
        }
    }

    private Tab _hoveredTab = null;
    public Tab HoveredTab
    {
        get => _hoveredTab;
        set
        {
            if (_hoveredTab == value)
                return;

            _hoveredTab = value;
            HoveredTabChanged?.Invoke(this, new TabEventArgs(_hoveredTab));
        }
    }

    public int FontIndex { get; set; }

    public bool DisposeTexturesOnTabRemove { get; set; }

    private Color? _textColor;

    public Color TextColor
    {
        get => _textColor ?? UISettings.ActiveSettings.AltColor;
        set => _textColor = value;
    }

    private Color? _textColorDisabled;

    public Color TextColorDisabled
    {
        get => _textColorDisabled ?? UISettings.ActiveSettings.DisabledItemColor;
        set => _textColorDisabled = value;
    }

    public List<Tab> Tabs { get; set; } = new List<Tab>();

    public EnhancedSoundEffect HoverSound { get; set; }

    // Global default click sound for tabs (can be overridden per-tab)
    public EnhancedSoundEffect ClickSound { get; set; }

    // Direction of tab drawing / layout
    public enum TabDirectionType { Horizontal, Vertical }
    public TabDirectionType TabDirection { get; set; } = TabDirectionType.Horizontal;

    // Global default resources (applied when per-tab value missing)
    private Texture2D _defaultIdleTexture;
    private Texture2D _defaultClickTexture;
    private string _defaultClickSoundName;
    private string _defaultTabText;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void RemoveTab(int index)
    {
        if (index < 0 || index >= Tabs.Count)
            throw new ArgumentOutOfRangeException($"{nameof(RemoveTab)}: Tab index out of range: {index}");

        Tab tab = Tabs[index];
        RemoveTab(tab);
    }

    public void RemoveTab(string text)
    {
        Tab tab = Tabs.Find(t => t.Text == text);
        if (tab != null)
            RemoveTab(tab);
    }

    private void FireSelectedTabChangedEvent(Tab previousTab)
    {
        if (_selectedIndexChangeDepth == 0)
        {
            if (SelectedTab != previousTab)
                SelectedTabChanged?.Invoke(this, new TabEventArgs(SelectedTab));
        }
    }

    public void RemoveTab(Tab tab)
    {
        int index = Tabs.IndexOf(tab);

        if (tab == null)
            throw new ArgumentNullException(nameof(tab));

        if (index < 0)
            throw new ArgumentException("The given tab does not belong to this tab control!");

        if (DisposeTexturesOnTabRemove)
        {
            tab.DefaultTexture?.Dispose();
            tab.PressedTexture?.Dispose();
        }

        // Record selection state. Only fire event handlers after the removal has been completed,
        // so there's no order-of-operation issues if an event handler wants to change to another tab.
        bool decrementSelectedTab = false;

        Tab previousTab = SelectedTab;

        if (SelectedTabIndex >= index)
            decrementSelectedTab = true;

        Tabs.RemoveAt(index);

        if (previousTab == tab)
        {
            _selectedTabIndex = -1;
            FireSelectedIndexChangedEvent();
            FireSelectedTabChangedEvent(previousTab);
        }
        else if (decrementSelectedTab)
        {
            _selectedTabIndex--;
            FireSelectedIndexChangedEvent();
            FireSelectedTabChangedEvent(previousTab);
        }

        if (HoveredTab == tab)
            HoveredTab = null;

        UpdateLayout();
    }

    public void AddTab(string text, Texture2D defaultTexture, Texture2D pressedTexture)
    {
        AddTab(text, defaultTexture, pressedTexture, true, null);
    }

    public void AddTab(string text, Texture2D defaultTexture, Texture2D pressedTexture, bool selectable, object tag)
    {
        if (defaultTexture == null)
            throw new ArgumentNullException($"{nameof(AddTab)} requires a default texture to be specified.");

        var tab = new Tab(text, defaultTexture, pressedTexture, selectable);
        tab.Tag = tag;
        Tabs.Add(tab);

        RecalculateTabTextPosition(tab);
        UpdateLayout();
    }

    private void RecalculateTabTextPosition(Tab tab)
    {
        if (tab.DefaultTexture == null || string.IsNullOrEmpty(tab.Text))
            return;

        int fontToUse = tab.FontIndex ?? FontIndex;
        Vector2 textSize = Renderer.GetTextDimensions(tab.Text, fontToUse);
        tab.TextXPosition = (tab.DefaultTexture.Width - (int)textSize.X) / 2;
        tab.TextYPosition = Renderer.GetTextYPadding(tab.Text, fontToUse, tab.DefaultTexture.Height);
    }

    /// <summary>
    /// Localizes tab text parsed from INI. Override to provide translation support.
    /// </summary>
    /// <param name="text">The raw text value from the INI.</param>
    /// <param name="key">The full INI key (e.g. "TabText" or "TabN.Text").</param>
    /// <returns>The localized text, or the original text if no localization is available.</returns>
    protected virtual string LocalizeTabText(string text, string key) => text;

    public void AddTab(Tab tab)
    {
        if (tab == null)
            throw new ArgumentNullException(nameof(tab));

        Tabs.Add(tab);
        RecalculateTabTextPosition(tab);
        UpdateLayout();
    }

    protected override void ParseControlINIAttribute(IniFile iniFile, string key, string value)
    {
        switch (key)
        {
            case "RemapColor":
            case "TextColor":
                TextColor = AssetLoader.GetColorFromString(value);
                return;
            case "TextColorDisabled":
                TextColorDisabled = AssetLoader.GetColorFromString(value);
                return;
        }

        // Handle disabling tab by index: DisabledTabIndexN=yes
        if (key.StartsWith("DisabledTabIndex", StringComparison.InvariantCulture))
        {
            if (!int.TryParse(key.Substring("DisabledTabIndex".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                return;

            if (index < 0 || index >= Tabs.Count)
                return;

            bool disabled = Conversions.BooleanFromString(value, false);
            Tabs[index].Selectable = !disabled;

            // If the tab that became disabled was the selected one, move to next selectable
            if (disabled && index == SelectedTabIndex)
            {
                int next = -1;
                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i].Selectable)
                    {
                        next = i;
                        break;
                    }
                }

                if (next != -1)
                    SelectedTabIndex = next;
            }

            return;
        }

        // Handle removing a tab by index: RemoveTabIndexN=yes
        if (key.StartsWith("RemoveTabIndex", StringComparison.InvariantCulture))
        {
            if (int.TryParse(key.Substring("RemoveTabIndex".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int removeIndex) &&
                Conversions.BooleanFromString(value, false))
            {
                RemoveTab(removeIndex);
            }

            return;
        }

        // Global: SetTabCount=N
        if (key.Equals("SetTabCount", StringComparison.InvariantCultureIgnoreCase))
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                return;

            if (count < 0) count = 0;

            // If we need to increase the number of tabs, add placeholders using defaults
            while (Tabs.Count < count)
            {
                var idle = _defaultIdleTexture;
                var pressed = _defaultClickTexture;
                string text = _defaultTabText ?? string.Empty;
                var tab = new Tab(text, idle, pressed, true);
                // Do not assign global click sound to per-tab ClickSound here.
                Tabs.Add(tab);
                RecalculateTabTextPosition(tab);
            }

            // If we need to shrink, remove extras
            while (Tabs.Count > count)
            {
                Tabs.RemoveAt(Tabs.Count - 1);
            }

            UpdateLayout();

            return;
        }

        // Global TabDirection
        if (key.Equals("TabDirection", StringComparison.InvariantCultureIgnoreCase))
        {
            if (string.Equals(value, "Vertical", StringComparison.InvariantCultureIgnoreCase))
                TabDirection = TabDirectionType.Vertical;
            else
                TabDirection = TabDirectionType.Horizontal;

            return;
        }

        // Global ClickSoundEffect - set control's default and apply to tabs missing per-tab sound
        if (key.Equals("ClickSoundEffect", StringComparison.InvariantCultureIgnoreCase))
        {
            if (!string.IsNullOrEmpty(value))
            {
                ClickSound = new EnhancedSoundEffect(value);
                _defaultClickSoundName = value;
                // Do not populate per-tab ClickSound here. Per-tab sounds override global and
                // should only be created when explicitly specified for the tab.
            }

            return;
        }

        // Global IdleTexture
        if (key.Equals("IdleTexture", StringComparison.InvariantCultureIgnoreCase))
        {
            if (!string.IsNullOrEmpty(value))
            {
                _defaultIdleTexture = AssetLoader.LoadTexture(value);

                foreach (var t in Tabs)
                {
                    if (t.DefaultTexture == null)
                        t.DefaultTexture = _defaultIdleTexture;
                    RecalculateTabTextPosition(t);
                }
                UpdateLayout();
            }

            return;
        }

        // Global ClickTexture (pressed texture)
        if (key.Equals("ClickTexture", StringComparison.InvariantCultureIgnoreCase) ||
            key.Equals("PressedTexture", StringComparison.InvariantCultureIgnoreCase))
        {
            if (!string.IsNullOrEmpty(value))
            {
                _defaultClickTexture = AssetLoader.LoadTexture(value);

                foreach (var t in Tabs)
                {
                    if (t.PressedTexture == null)
                        t.PressedTexture = _defaultClickTexture;
                }
                UpdateLayout();
            }

            return;
        }

        // Global FontIndex
        if (key.Equals("FontIndex", StringComparison.InvariantCultureIgnoreCase))
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fi))
            {
                FontIndex = fi;
                foreach (var t in Tabs)
                {
                    RecalculateTabTextPosition(t);
                }
                UpdateLayout();
            }

            return;
        }

        // Global TabText: default text for tabs created without specific TabN.Text
        if (key.Equals("TabText", StringComparison.InvariantCultureIgnoreCase))
        {
            _defaultTabText = value;
            string localizedDefault = LocalizeTabText(value, key);
            foreach (var t in Tabs)
            {
                if (string.IsNullOrEmpty(t.Text))
                {
                    t.Text = localizedDefault;
                    RecalculateTabTextPosition(t);
                }
            }

            UpdateLayout();

            return;
        }

        // Micro (per-tab) settings: TabN.Property
        if (key.StartsWith("Tab", StringComparison.InvariantCulture))
        {
            // Expected format: Tab{index}.{Property}
            int dotIndex = key.IndexOf('.');
            if (dotIndex > 3) // ensures there is at least "TabX.Prop"
            {
                string indexStr = key.Substring(3, dotIndex - 3);
                if (int.TryParse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tabIndex))
                {
                    if (tabIndex < 0)
                        return;

                    // Ensure the tab exists (expand using defaults if necessary)
                    if (tabIndex >= Tabs.Count)
                    {
                        int need = tabIndex + 1 - Tabs.Count;
                        for (int i = 0; i < need; i++)
                        {
                            var idle = _defaultIdleTexture;
                            var pressed = _defaultClickTexture;
                            var newTab = new Tab(_defaultTabText ?? string.Empty, idle, pressed, true);
                            Tabs.Add(newTab);
                            RecalculateTabTextPosition(newTab);
                        }
                    }

                    Tab tab = Tabs[tabIndex];
                    string property = key.Substring(dotIndex + 1);

                    switch (property)
                    {
                        case "ClickSoundEffect":
                            if (!string.IsNullOrEmpty(value))
                                tab.ClickSound = new EnhancedSoundEffect(value);
                            break;
                        case "IdleTexture":
                            if (!string.IsNullOrEmpty(value))
                                tab.DefaultTexture = AssetLoader.LoadTexture(value);
                            RecalculateTabTextPosition(tab);
                            UpdateLayout();
                            break;
                        case "ClickTexture":
                        case "PressedTexture":
                            if (!string.IsNullOrEmpty(value))
                                tab.PressedTexture = AssetLoader.LoadTexture(value);
                            UpdateLayout();
                            break;
                        case "FontIndex":
                            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tfi))
                                tab.FontIndex = tfi;
                            RecalculateTabTextPosition(tab);
                            UpdateLayout();
                            break;
                        case "Text":
                        case "TabText":
                            tab.Text = LocalizeTabText(value, key);
                            RecalculateTabTextPosition(tab);
                            UpdateLayout();
                            break;
                        case "Selectable":
                            tab.Selectable = Conversions.BooleanFromString(value, true);
                            break;
                        default:
                            // Unknown per-tab property -> fallthrough to base
                            base.ParseControlINIAttribute(iniFile, key, value);
                            return;
                    }

                    return;
                }
            }
        }

        // Fall back to base parser for other keys
        base.ParseControlINIAttribute(iniFile, key, value);
    }

    private Tab GetTabOnCursor()
    {
        Point p = GetCursorPoint();
        if (p.Y < 0 || p.Y >= Height)
            return null;

        if (TabDirection == TabDirectionType.Horizontal)
        {
            int w = 0;
            foreach (Tab tab in Tabs)
            {
                w += tab.Width;

                if (p.X < w)
                    return tab;
            }
        }
        else
        {
            int h = 0;
            foreach (Tab tab in Tabs)
            {
                h += tab.Height;

                if (p.Y < h)
                    return tab;
            }
        }

        return null;
    }

    public override void OnLeftClick(InputEventArgs inputEventArgs)
    {
        inputEventArgs.Handled = true;
        Tab tabOnCursor = GetTabOnCursor();
        if (tabOnCursor != null && tabOnCursor.Selectable && Enabled)
        {
            SelectedTab = tabOnCursor;

            // Play per-tab sound if present, otherwise play global click sound
            if (tabOnCursor.ClickSound != null)
                tabOnCursor.ClickSound.Play();
            else
                ClickSound?.Play();
        }

        base.OnLeftClick(inputEventArgs);
    }

    public override void OnMouseMove()
    {
        Tab newHoveredTab = GetTabOnCursor();
        if (newHoveredTab != HoveredTab)
        {
            HoveredTab = newHoveredTab;
            if (HoveredTab != null && HoveredTab.Selectable)
            {
                HoverSound?.Play();
            }
        }

        base.OnMouseMove();
    }

    public override void OnMouseLeave()
    {
        HoveredTab = null;
        base.OnMouseLeave();
    }

    public virtual void DrawTab(GameTime gameTime, int offset, Tab tab)
    {
        Texture2D texture = tab == SelectedTab ? tab.PressedTexture ?? tab.DefaultTexture : tab.DefaultTexture;

        if (texture == null)
            return;

        Point drawPoint = TabDirection == TabDirectionType.Horizontal
            ? new Point(offset, 0)
            : new Point(0, offset);

        DrawTexture(texture, drawPoint, RemapColor);

        DrawStringWithShadow(tab.Text, tab.FontIndex ?? FontIndex,
            new Vector2(drawPoint.X + tab.TextXPosition, drawPoint.Y + tab.TextYPosition),
            tab.Selectable && Enabled ? TextColor : TextColorDisabled);
    }

    public override void Draw(GameTime gameTime)
    {
        int offset = 0;

        for (int i = 0; i < Tabs.Count; i++)
        {
            Tab tab = Tabs[i];
            DrawTab(gameTime, offset, tab);

            offset += TabDirection == TabDirectionType.Horizontal ? tab.Width : tab.Height;
        }
    }
}

public class Tab
{
    public Tab() { }

    public Tab(string text, Texture2D defaultTexture, Texture2D pressedTexture, bool selectable)
    {
        Text = text;
        DefaultTexture = defaultTexture;
        PressedTexture = pressedTexture;
        Selectable = selectable;
    }

    /// <summary>
    /// The width of the tab based on the tab's texture.
    /// </summary>
    public int Width => DefaultTexture?.Width ?? 0;

    /// <summary>
    /// The height of the tab based on the tab's texture.
    /// </summary>
    public int Height => DefaultTexture?.Height ?? 0;

    public Texture2D DefaultTexture { get; set; }

    public Texture2D PressedTexture { get; set; }

    public string Text { get; set; }

    public bool Selectable { get; set; }

    public int TextXPosition { get; set; }

    public int TextYPosition { get; set; }

    // Per-tab override for click sound (takes precedence over control ClickSound)
    public EnhancedSoundEffect ClickSound { get; set; }

    // Per-tab font index override (nullable => use control FontIndex)
    public int? FontIndex { get; set; }

    public object Tag { get; set; }
}
