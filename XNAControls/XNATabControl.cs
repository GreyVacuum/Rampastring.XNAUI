using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI.FontManagement;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rampastring.XNAUI.XNAControls;

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

    public delegate void SelectedIndexChangedEventHandler(object sender, EventArgs e);
    public event SelectedIndexChangedEventHandler SelectedIndexChanged;

    private int _selectedTab = 0;
    public int SelectedTab
    {
        get { return _selectedTab; }
        set
        {
            if (_selectedTab == value)
                return;

            _selectedTab = value;

            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int FontIndex { get; set; }

    public bool DisposeTexturesOnTabRemove { get; set; }

    private Color? _textColor;

    public Color TextColor
    {
        get => _textColor ?? UISettings.ActiveSettings.AltColor;
        set { _textColor = value; }
    }

    private Color? _textColorDisabled;

    public Color TextColorDisabled
    {
        get => _textColorDisabled ?? UISettings.ActiveSettings.DisabledItemColor;
        set { _textColorDisabled = value; }
    }

    private List<Tab> Tabs = new List<Tab>();

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

    public void MakeSelectable(int index)
    {
        Tabs[index].Selectable = true;
    }

    public void MakeUnselectable(int index)
    {
        Tabs[index].Selectable = false;
    }

    public void RemoveTab(int index)
    {
        if (DisposeTexturesOnTabRemove)
        {
            Tabs[index].DefaultTexture?.Dispose();
            Tabs[index].PressedTexture?.Dispose();
        }

        Tabs.RemoveAt(index);
        UpdateLayout();
    }

    public void RemoveTab(string text)
    {
        int index = Tabs.FindIndex(t => t.Text == text);

        Tabs.RemoveAt(index);
        UpdateLayout();
    }

    public void AddTab(string text, Texture2D defaultTexture, Texture2D pressedTexture)
    {
        AddTab(text, defaultTexture, pressedTexture, true);
    }

    public void AddTab(string text, Texture2D defaultTexture, Texture2D pressedTexture, bool selectable)
    {
        var tab = new Tab(text, defaultTexture, pressedTexture, selectable);
        Tabs.Add(tab);

        // Use either tab-specific font index if set, otherwise control FontIndex
        int fontToUse = tab.FontIndex ?? FontIndex;

        if (defaultTexture != null && !string.IsNullOrEmpty(text))
        {
            Vector2 textSize = Renderer.GetTextDimensions(text, fontToUse);
            tab.TextXPosition = (defaultTexture.Width - (int)textSize.X) / 2;
            tab.TextYPosition = (defaultTexture.Height - (int)textSize.Y) / 2;
        }

        // Apply global width/height calculation for horizontal layout.
        if (TabDirection == TabDirectionType.Horizontal)
        {
            if (defaultTexture != null)
                Width += defaultTexture.Width;

            if (defaultTexture != null)
                Height = defaultTexture.Height;
        }
        else
        {
            // vertical layout: width is max of textures, height is cumulative
            if (defaultTexture != null)
            {
                Width = Math.Max(Width, defaultTexture.Width);
                Height += defaultTexture.Height;
        }

        UpdateLayout();
        }
    }

    private void RecalculateTabTextPosition(Tab tab)
    {
        if (tab.DefaultTexture == null || string.IsNullOrEmpty(tab.Text))
            return;

        int fontToUse = tab.FontIndex ?? FontIndex;
        Vector2 textSize = Renderer.GetTextDimensions(tab.Text, fontToUse);
        tab.TextXPosition = (tab.DefaultTexture.Width - (int)textSize.X) / 2;
        tab.TextYPosition = (tab.DefaultTexture.Height - (int)textSize.Y) / 2;
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
            if (disabled && index == SelectedTab)
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
                    SelectedTab = next;
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

            // Recalculate Width/Height from scratch for current TabDirection
            Width = 0;
            Height = 0;
            foreach (var t in Tabs)
            {
                if (t.DefaultTexture == null)
                    continue;

                if (TabDirection == TabDirectionType.Horizontal)
                {
                    Width += t.DefaultTexture.Width;
                    Height = t.DefaultTexture.Height; // assume uniform height
                }
                else
                {
                    Width = Math.Max(Width, t.DefaultTexture.Width);
                    Height += t.DefaultTexture.Height;
                }
            }

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
            foreach (var t in Tabs)
            {
                if (string.IsNullOrEmpty(t.Text))
                {
                    t.Text = _defaultTabText;
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
                            tab.Text = value;
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

    public override void OnLeftClick(InputEventArgs inputEventArgs)
    {
        base.OnLeftClick(inputEventArgs);
        inputEventArgs.Handled = true;

        Point p = GetCursorPoint();

        if (TabDirection == TabDirectionType.Horizontal)
        {
            int w = 0;
            int i = 0;
            foreach (Tab tab in Tabs)
            {
                w += tab.DefaultTexture?.Width ?? 0;

                if (p.X < w)
                {
                    if (tab.Selectable && Enabled)
                    {
                        // Play per-tab sound if present, otherwise play global click sound
                        if (tab.ClickSound != null)
                            tab.ClickSound.Play();
                        else
                            ClickSound?.Play();

                        SelectedTab = i;
                    }

                    return;
                }

                i++;
            }
        }
        else
        {
            int h = 0;
            int i = 0;
            foreach (Tab tab in Tabs)
            {
                h += tab.DefaultTexture?.Height ?? 0;

                if (p.Y < h)
                {
                    if (tab.Selectable && Enabled)
                    {
                        if (tab.ClickSound != null)
                            tab.ClickSound.Play();
                        else
                            ClickSound?.Play();

                        SelectedTab = i;
                    }

                    return;
                }

                i++;
            }
        }
    }

    public override void Draw(GameTime gameTime)
    {
        int x = 0;
        int y = 0;

        for (int i = 0; i < Tabs.Count; i++)
        {
            Tab tab = Tabs[i];

            Texture2D texture = i == SelectedTab ? tab.PressedTexture ?? tab.DefaultTexture : tab.DefaultTexture;

            if (texture == null)
                continue;

            Point drawPoint;
            if (TabDirection == TabDirectionType.Horizontal)
            {
                drawPoint = new Point(x, 0);
                DrawTexture(texture, drawPoint, RemapColor);

                DrawStringWithShadow(tab.Text, FontIndex,
                    new Vector2(x + tab.TextXPosition, tab.TextYPosition),
                    tab.Selectable && Enabled ? TextColor : TextColorDisabled);

                x += texture.Width;
            }
            else
            {
                drawPoint = new Point(0, y);
                DrawTexture(texture, drawPoint, RemapColor);

                DrawStringWithShadow(tab.Text, FontIndex,
                    new Vector2(drawPoint.X + tab.TextXPosition, drawPoint.Y + tab.TextYPosition),
                    tab.Selectable && Enabled ? TextColor : TextColorDisabled);

                y += texture.Height;
            }
        }
    }
}

internal class Tab
{
    public Tab() { }

    public Tab(string text, Texture2D defaultTexture, Texture2D pressedTexture, bool selectable)
    {
        Text = text;
        DefaultTexture = defaultTexture;
        PressedTexture = pressedTexture;
        Selectable = selectable;
    }

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
}
