using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI.Extensions;

namespace Rampastring.XNAUI.FontManagement;

/// <summary>
/// Manages font loading and rendering for the UI system.
/// Supports both SpriteFont and TrueType fonts with automatic fallback.
/// </summary>
/// <remarks>
/// <para>
/// For TrueType fonts, FontManager creates a separate FontSystem for each font index.
/// Each FontSystem has a primary font (specified via Path) and optional per-font fallback
/// chain (via Fallback=N pointing to another font index). When a character is not found in
/// the primary font, it falls back through the chain in order.
/// </para>
/// <para>
/// The Fonts.ini file format supports:
/// <list type="bullet">
/// <item>[TextShaping] - Optional HarfBuzz text shaping configuration</item>
/// <item>[Fonts] - Font index definitions with Size, Type, optional Path, and optional Fallback</item>
/// </list>
/// </para>
/// </remarks>
public static class FontManager
{
    private const int DefaultShapedTextCacheSize = 100;

    private static List<IFont> fonts;
    private static List<FontSystem> fontSystems = new();
    private static TextShapingSettings textShapingSettings = new();
    private static FontRenderingSettings fontRenderingSettings = new();

    /// <summary>
    /// When set before <see cref="LoadFonts"/> runs, skips the Fonts.ini search and
    /// loads only legacy SpriteFontN.xnb assets. Lets clients offer an opt-out from
    /// TrueType rendering.
    /// </summary>
    public static bool UseLegacySpriteFonts { get; set; }

    public static void Initialize()
    {
        fonts = [];
    }

    /// <summary>
    /// Gets the current text shaping settings.
    /// </summary>
    public static TextShapingSettings GetTextShapingSettings() => textShapingSettings;

    /// <summary>
    /// Gets the current font rendering settings.
    /// </summary>
    public static FontRenderingSettings GetFontRenderingSettings() => fontRenderingSettings;

    /// <summary>
    /// Checks if text shaping is currently enabled.
    /// </summary>
    public static bool IsTextShapingEnabled() => textShapingSettings.Enabled;

    /// <summary>
    /// Creates a new FontSystem with current text shaping settings.
    /// </summary>
    private static FontSystem CreateFontSystem()
    {
        var settings = new FontSystemSettings
        {
            KernelWidth = fontRenderingSettings.KernelWidth,
            KernelHeight = fontRenderingSettings.KernelHeight,
            FontResolutionFactor = fontRenderingSettings.FontResolutionFactor,
            TextureWidth = fontRenderingSettings.TextureWidth,
            TextureHeight = fontRenderingSettings.TextureHeight,
            GlyphRenderResult = fontRenderingSettings.GlyphRenderResult,
            UseEmToPixelsScale = true
        };

        if (textShapingSettings.Enabled)
        {
            var shaper = new HarfBuzzTextShaper
            {
                EnableBiDi = textShapingSettings.EnableBiDi
            };
            settings.TextShaper = shaper;
            settings.ShapedTextCacheSize = textShapingSettings.CacheSize;
        }

        return new FontSystem(settings);
    }

    public static Vector2 MeasureString(string text, int fontIndex)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");

        return fonts[fontIndex].MeasureString(text);
    }

    /// <summary>
    /// Loads fonts from the first Fonts.ini found in asset search paths.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Loading happens in two phases:
    /// </para>
    /// <para>
    /// Phase 1: Load configuration from the first Fonts.ini:
    /// - [TextShaping] settings
    /// - [Fonts] definitions (type, path, size, fallback)
    /// </para>
    /// <para>
    /// Phase 2: Create font indexes:
    /// - For TrueType fonts: Create a FontSystem with primary font first, then follow the per-font fallback chain
    /// - For SpriteFonts: Load the .xnb file
    /// </para>
    /// </remarks>
    /// <param name="contentManager">Content manager used to load SpriteFont assets.</param>
    /// <param name="minimumFontResolutionFactor">
    /// Optional minimum rasterization factor required by the display scale. The value
    /// configured in <c>Fonts.ini</c> is preserved when it is higher.
    /// </param>
    public static void LoadFonts(ContentManager contentManager, float? minimumFontResolutionFactor = null)
    {
        fonts ??= [];
        fonts.Clear();
        fontSystems.Clear();

        // Reset text shaping and rendering settings
        textShapingSettings = new TextShapingSettings();
        fontRenderingSettings = new FontRenderingSettings();

        string originalContentRoot = contentManager.RootDirectory;
        bool fontsIniFound = false;

        if (UseLegacySpriteFonts)
        {
            Logger.Log("FontManager: UseLegacySpriteFonts is set, skipping Fonts.ini search");
        }
        else
        {
            foreach (string searchPath in AssetLoader.AssetSearchPaths)
            {
                string baseDir = SafePath.GetDirectory(searchPath).FullName;
                string iniPath = Path.Combine(baseDir, "Fonts.ini");

                if (File.Exists(iniPath))
                {
                    Logger.Log($"FontManager: Loading fonts from {iniPath}");
                    LoadFontsFromIni(iniPath, contentManager, searchPath, baseDir, minimumFontResolutionFactor);
                    fontsIniFound = true;
                    // Stop after first Fonts.ini found
                    break;
                }
            }
        }

        if (!fontsIniFound && minimumFontResolutionFactor.HasValue)
            fontRenderingSettings.FontResolutionFactor =
                Math.Max(fontRenderingSettings.FontResolutionFactor, minimumFontResolutionFactor.Value);

        // Fall back to legacy SpriteFont loading if no Fonts.ini found
        if (!fontsIniFound)
        {
            Logger.Log("FontManager: No Fonts.ini found, attempting legacy SpriteFont loading");
            foreach (string searchPath in AssetLoader.AssetSearchPaths)
            {
                string baseDir = SafePath.GetDirectory(searchPath).FullName;
                int fontsBeforeLoad = fonts.Count;
                LoadLegacySpriteFonts(contentManager, searchPath, baseDir);

                if (fonts.Count > fontsBeforeLoad)
                {
                    // Stop after first path with legacy fonts
                    break;
                }
            }
        }

        contentManager.SetRootDirectory(originalContentRoot);

        Logger.Log($"FontManager: Loaded {fonts.Count} font indexes with {fontSystems.Count} FontSystems");
    }

    /// <summary>
    /// Loads fonts from a specific Fonts.ini file.
    /// </summary>
    private static void LoadFontsFromIni(string iniPath, ContentManager contentManager, string searchPath, string baseDir,
        float? minimumFontResolutionFactor)
    {
        var iniFile = new IniFile(iniPath);

        // Load text shaping settings
        if (iniFile.SectionExists("TextShaping"))
        {
            LoadTextShapingSettings(iniFile);
        }

        // Load font rendering settings
        if (iniFile.SectionExists("FontRendering"))
        {
            LoadFontRenderingSettings(iniFile);
        }

        if (minimumFontResolutionFactor.HasValue)
            fontRenderingSettings.FontResolutionFactor =
                Math.Max(fontRenderingSettings.FontResolutionFactor, minimumFontResolutionFactor.Value);

        CreateFontIndexesFromIni(iniFile, contentManager, searchPath, baseDir);
    }

    private static void LoadTextShapingSettings(IniFile iniFile)
    {
        textShapingSettings.Enabled = iniFile.GetBooleanValue("TextShaping", "Enabled", false);

        // When shaping is enabled, probe the native HarfBuzz library once and disable shaping if it
        // cannot be loaded here - a missing native binary, or a path the .NET Framework loader cannot
        // represent - so the client renders unshaped instead of crashing during font loading (which
        // would otherwise throw "Unable to load library 'libHarfBuzzSharp'").
        if (textShapingSettings.Enabled && !IsNativeTextShaperAvailable())
            textShapingSettings.Enabled = false;

        textShapingSettings.EnableBiDi = iniFile.GetBooleanValue("TextShaping", "EnableBiDi", true);
        textShapingSettings.CacheSize = iniFile.GetIntValue("TextShaping", "CacheSize", DefaultShapedTextCacheSize);

        if (textShapingSettings.CacheSize < 1)
            textShapingSettings.CacheSize = DefaultShapedTextCacheSize;

        Logger.Log($"FontManager: Text shaping settings: Enabled={textShapingSettings.Enabled}, BiDi={textShapingSettings.EnableBiDi}, CacheSize={textShapingSettings.CacheSize}");
    }

    /// <summary>
    /// Probes whether the native HarfBuzz library can actually be loaded in the current process.
    /// The managed HarfBuzzSharp assembly can load successfully while its native counterpart
    /// (libHarfBuzzSharp) cannot - for example when the native binary was not deployed, or when the
    /// .NET Framework build runs from a path that cannot be represented in the system ANSI code page.
    /// Detecting this once, up front, lets the font system fall back to unshaped rendering with a
    /// clear log entry instead of throwing in the middle of font loading.
    /// </summary>
    private static bool IsNativeTextShaperAvailable()
    {
        try
        {
            using var probe = new HarfBuzzSharp.Buffer();
            return true;
        }
        catch (Exception ex)
        {
            // The real cause (e.g. "Unable to load library 'libHarfBuzzSharp'") is typically wrapped
            // in a TypeInitializationException from HarfBuzzApi's static initializer; unwrap it so the
            // log states the actual reason rather than a generic type-initializer message.
            Exception rootCause = ex;
            while (rootCause.InnerException != null)
                rootCause = rootCause.InnerException;

            Logger.Log($"FontManager: Native HarfBuzz text shaper is unavailable; continuing without text shaping. Reason: {rootCause.Message}");
            return false;
        }
    }

    private static void LoadFontRenderingSettings(IniFile iniFile)
    {
        int kernelWidth = iniFile.GetIntValue("FontRendering", "KernelWidth", 0);
        int kernelHeight = iniFile.GetIntValue("FontRendering", "KernelHeight", 0);
        float resolutionFactor = iniFile.GetSingleValue("FontRendering", "FontResolutionFactor", 1f);
        int textureWidth = iniFile.GetIntValue("FontRendering", "TextureWidth", 1024);
        int textureHeight = iniFile.GetIntValue("FontRendering", "TextureHeight", 1024);
        string glyphResultStr = iniFile.GetStringValue("FontRendering", "GlyphRenderResult", nameof(GlyphRenderResult.Premultiplied));

        if (kernelWidth < 0)
            kernelWidth = 0;
        if (kernelHeight < 0)
            kernelHeight = 0;
        if (resolutionFactor < 0f)
            resolutionFactor = 0f;
        if (textureWidth < 1)
            textureWidth = 1;
        if (textureHeight < 1)
            textureHeight = 1;

        if (!Enum.TryParse<GlyphRenderResult>(glyphResultStr, true, out var glyphResult))
            glyphResult = GlyphRenderResult.Premultiplied;

        fontRenderingSettings.KernelWidth = kernelWidth;
        fontRenderingSettings.KernelHeight = kernelHeight;
        fontRenderingSettings.FontResolutionFactor = resolutionFactor;
        fontRenderingSettings.TextureWidth = textureWidth;
        fontRenderingSettings.TextureHeight = textureHeight;
        fontRenderingSettings.GlyphRenderResult = glyphResult;

        Logger.Log($"FontManager: Font rendering settings: KernelWidth={fontRenderingSettings.KernelWidth}, KernelHeight={fontRenderingSettings.KernelHeight}, FontResolutionFactor={fontRenderingSettings.FontResolutionFactor}, TextureSize={fontRenderingSettings.TextureWidth}x{fontRenderingSettings.TextureHeight}, GlyphRenderResult={fontRenderingSettings.GlyphRenderResult}");
    }

    /// <summary>
    /// Creates FontIndex entries from a Fonts.ini file.
    /// For each TrueType font, creates a separate FontSystem with the primary font first,
    /// then follows the per-font fallback chain to add additional fonts.
    /// </summary>
    private static void CreateFontIndexesFromIni(IniFile iniFile, ContentManager contentManager, string searchPath, string baseDir)
    {
        int fontCount = iniFile.GetIntValue("Fonts", "Count", 0);

        Logger.Log($"FontManager: Creating {fontCount} font indexes");

        // Pre-parse all font configs so fallback references can be resolved
        var fontConfigs = new List<FontConfig>(fontCount);
        for (int i = 0; i < fontCount; i++)
        {
            string section = $"Font{i}";
            string fontPath = iniFile.GetStringValue(section, "Path", "");
            int size = iniFile.GetIntValue(section, "Size", 16);
            string fontTypeStr = iniFile.GetStringValue(section, "Type", nameof(FontType.SpriteFont));
            int fallback = iniFile.GetIntValue(section, "Fallback", -1);

            if (!Enum.TryParse<FontType>(fontTypeStr, true, out var fontType))
                throw new Exception($"Invalid font type for {section}: {fontTypeStr}");

            fontConfigs.Add(new FontConfig(fontPath, size, fontType, fallback));
        }

        for (int i = 0; i < fontCount; i++)
        {
            FontConfig config = fontConfigs[i];

            switch (config.FontType)
            {
                case FontType.TrueType:
                    CreateTrueTypeFontIndex(i, config, fontConfigs, searchPath);
                    break;

                case FontType.SpriteFont:
                    contentManager.SetRootDirectory(baseDir);
                    string sfName = Path.GetFileNameWithoutExtension(config.Path);
                    LoadSpriteFont(contentManager, searchPath, sfName);
                    break;
            }
        }
    }

    /// <summary>
    /// Creates a TrueType font index with its own FontSystem.
    /// The FontSystem contains the primary font first, then fonts from
    /// the fallback chain (each <c>[FontN]</c> can point to another via <c>Fallback=X</c>).
    /// </summary>
    private static void CreateTrueTypeFontIndex(int fontIndex, FontConfig config, List<FontConfig> allConfigs, string searchPath)
    {
        FontSystem fontSystem = CreateFontSystem();
        fontSystem.DefaultCharacter = '?';
        fontSystems.Add(fontSystem);

        bool hasPrimaryFont = false;

        // Add primary font first
        if (!string.IsNullOrEmpty(config.Path))
        {
            string fullPath = SafePath.GetFile(searchPath, config.Path).FullName;
            if (File.Exists(fullPath))
            {
                try
                {
                    fontSystem.AddFont(File.ReadAllBytes(fullPath));
                    Logger.Log($"FontManager: Font{fontIndex} - Added primary font: {config.Path}");
                    hasPrimaryFont = true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"FontManager: Font{fontIndex} - Failed to load primary font {config.Path}: {ex.Message}");
                }
            }
            else
            {
                throw new Exception($"FontManager: Font{fontIndex} - Primary font not found: {fullPath}");
            }
        }

        // Follow the per-font fallback chain
        int fallbacksAdded = 0;
        var visited = new HashSet<int> { fontIndex };
        int current = config.Fallback;

        while (current >= 0 && current < allConfigs.Count && visited.Add(current))
        {
            FontConfig fallbackConfig = allConfigs[current];

            if (fallbackConfig.FontType != FontType.TrueType)
            {
                Logger.Log($"FontManager: Font{fontIndex} - Fallback {current} is not a TrueType font, stopping chain");
                break;
            }

            if (string.IsNullOrEmpty(fallbackConfig.Path))
            {
                Logger.Log($"FontManager: Font{fontIndex} - Fallback {current} has no path, stopping chain");
                break;
            }

            string fullPath = SafePath.GetFile(searchPath, fallbackConfig.Path).FullName;
            if (File.Exists(fullPath))
            {
                try
                {
                    fontSystem.AddFont(File.ReadAllBytes(fullPath));
                    fallbacksAdded++;
                    Logger.Log($"FontManager: Font{fontIndex} - Added fallback font from Font{current}: {fallbackConfig.Path}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"FontManager: Font{fontIndex} - Failed to load fallback font from Font{current}: {ex.Message}");
                    break;
                }
            }
            else
            {
                throw new Exception($"FontManager: Font{fontIndex} - Fallback font not found: {fullPath}");
            }

            current = fallbackConfig.Fallback;
        }

        if (fallbacksAdded > 0)
        {
            Logger.Log($"FontManager: Font{fontIndex} - Added {fallbacksAdded} fallback fonts via chain");
        }

        // Create the font wrapper
        if (hasPrimaryFont || fallbacksAdded > 0)
        {
            fonts.Add(new TTFFontWrapper(fontSystem.GetFont(config.Size)));
            string primaryInfo = hasPrimaryFont ? $"primary: {Path.GetFileName(config.Path)}" : "no primary";
            Logger.Log($"FontManager: Created FontIndex {fonts.Count - 1}: TrueType size {config.Size} ({primaryInfo}, {fallbacksAdded} fallbacks)");
        }
        else
        {
            Logger.Log($"FontManager: Font{fontIndex} - No fonts loaded (no primary and no fallbacks), skipping");
        }
    }

    private readonly struct FontConfig
    {
        public string Path { get; }
        public int Size { get; }
        public FontType FontType { get; }
        public int Fallback { get; }

        public FontConfig(string path, int size, FontType fontType, int fallback)
        {
            Path = path;
            Size = size;
            FontType = fontType;
            Fallback = fallback;
        }
    }

    /// <summary>
    /// Loads a SpriteFont and adds it to the font list.
    /// </summary>
    private static void LoadSpriteFont(ContentManager contentManager, string searchPath, string fontName)
    {
        if (SafePath.GetFile(searchPath, $"{fontName}.xnb").Exists)
        {
            var font = contentManager.Load<SpriteFont>(fontName);
            font.DefaultCharacter ??= '?';
            fonts.Add(new SpriteFontWrapper(font));
            Logger.Log($"FontManager: Created FontIndex {fonts.Count - 1}: SpriteFont {fontName}");
        }
        else
        {
            throw new Exception($"FontManager: SpriteFont file not found: {fontName}.xnb");
        }
    }

    /// <summary>
    /// Loads legacy SpriteFonts (SpriteFont0, SpriteFont1, etc.) from a search path.
    /// This method appends new fonts to the existing font list instead of replacing it.
    /// </summary>
    private static void LoadLegacySpriteFonts(ContentManager contentManager, string searchPath, string baseDir)
    {
        contentManager.SetRootDirectory(baseDir);

        int startIndex = fonts.Count;
        while (true)
        {
            string sfName = string.Format(CultureInfo.InvariantCulture, "SpriteFont{0}", fonts.Count - startIndex);
            if (!SafePath.GetFile(searchPath, FormattableString.Invariant($"{sfName}.xnb")).Exists)
                break;

            var font = contentManager.Load<SpriteFont>(sfName);
            font.DefaultCharacter ??= '?';
            fonts.Add(new SpriteFontWrapper(font));
            Logger.Log($"FontManager: Created FontIndex {fonts.Count - 1}: Legacy SpriteFont {sfName}");
        }
    }

    public static List<IFont> GetFontList() => fonts;

    public static string GetSafeString(string str, int fontIndex)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");

        return fonts[fontIndex].GetSafeString(str);
    }

    public static string GetStringWithLimitedWidth(string str, int fontIndex, int maxWidth)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");

        var font = fonts[fontIndex];

        if (str == null)
            throw new ArgumentNullException(nameof(str));

        if (string.IsNullOrEmpty(str) || font.MeasureString(str).X <= maxWidth)
            return str;

        // Binary search for the maximum number of characters that fit within maxWidth.
        // Assumes string width is monotonically non-decreasing as the string length increases,
        // which holds for all standard fonts.
        // This reduces complexity from O(n) to O(log n) compared to removing one character at a time.

        // Warning: Copilot said: The binary search relies on prefix width being monotonic with length, but that’s not guaranteed with kerning and/or HarfBuzz text shaping (both are used in this codebase). In such cases it’s possible for a longer prefix to measure narrower than a shorter one, making the <= maxWidth predicate non-monotonic and causing the search to return a prefix that is not the longest-fitting (or potentially not fitting at all, depending on the path).
        // We accept this risk for now.
        int low = 0;
        int high = str.Length - 1;

        while (low < high)
        {
            int mid = (low + high + 1) / 2; // Round up to avoid infinite loop when low + 1 == high
            if (font.MeasureString(str.SubstringSurrogateAware(0, mid)).X <= maxWidth)
                low = mid;
            else
                high = mid - 1;
        }

        return str.SubstringSurrogateAware(0, low);
    }

    public static TextParseReturnValue FixText(string text, int fontIndex, int width)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");

        IFont font = fonts[fontIndex];
        return TextParseReturnValue.FixText(font, width, text);
    }

    public static List<string> GetFixedTextLines(string text, int fontIndex, int width, bool splitWords = true, bool keepBlankLines = false)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");

        IFont font = fonts[fontIndex];
        return TextParseReturnValue.GetFixedTextLines(font, width, text, splitWords, keepBlankLines);
    }

    public static Vector2 GetTextDimensions(string text, int fontIndex)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");

        return fonts[fontIndex].MeasureString(text);
    }

    public static int GetTextYPadding(string text, int fontIndex, int containerHeight)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");

        return fonts[fontIndex].GetTextYPadding(containerHeight, text);
    }

    public static int GetSingleLineTextYPadding(int fontIndex, int containerHeight)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");
        return fonts[fontIndex].GetSingleLineTextYPadding(containerHeight);
    }

    public static void DrawString(SpriteBatch spriteBatch, string text, int fontIndex, Vector2 location, Color color, float scale = 1.0f, float depth = 0f)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");

        fonts[fontIndex].DrawString(spriteBatch, text, location, color, scale, depth);
    }

    public static void DrawStringWithShadow(SpriteBatch spriteBatch, string text, int fontIndex, Vector2 location, Color color, float scale = 1.0f, float shadowDistance = 1.0f, float depth = 0f)
    {
        if (fontIndex < 0 || fontIndex >= fonts.Count)
            throw new IndexOutOfRangeException($"Invalid font index. {fonts.Count} fonts loaded, requested index: {fontIndex}");

        Color shadowColor;
#if XNA
        shadowColor = new Color(0, 0, 0, color.A);
#else
        shadowColor = UISettings.ActiveSettings.TextShadowColor * (color.A / 255.0f);
#endif

        fonts[fontIndex].DrawString(spriteBatch, text, new Vector2(location.X + shadowDistance, location.Y + shadowDistance), shadowColor, scale, depth);
        fonts[fontIndex].DrawString(spriteBatch, text, location, color, scale, depth);
    }
}
