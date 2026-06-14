using FontStashSharp;

namespace Rampastring.XNAUI.FontManagement;

/// <summary>
/// Configuration for FontStashSharp glyph rasterization.
/// </summary>
public class FontRenderingSettings
{
    /// <summary>
    /// Horizontal blur kernel size used by FontStashSharp when rasterizing glyphs.
    /// Maps to <c>FontSystemSettings.KernelWidth</c>. Must be non-negative.
    /// </summary>
    public int KernelWidth { get; set; } = 4;

    /// <summary>
    /// Vertical blur kernel size used by FontStashSharp when rasterizing glyphs.
    /// Maps to <c>FontSystemSettings.KernelHeight</c>. Must be non-negative.
    /// </summary>
    public int KernelHeight { get; set; } = 4;

    /// <summary>
    /// Multiplier applied to the rasterization size of each glyph.
    /// Values > 1 produce sharper output when text is drawn at scales above 1.0
    /// at the cost of a larger atlas footprint.
    /// </summary>
    public float FontResolutionFactor { get; set; } = 5f;

    /// <summary>
    /// Width of each FontStashSharp atlas page, in pixels.
    /// </summary>
    public int TextureWidth { get; set; } = 1024;

    /// <summary>
    /// Height of each FontStashSharp atlas page, in pixels.
    /// </summary>
    public int TextureHeight { get; set; } = 1024;

    /// <summary>
    /// How rasterized glyph pixels are produced.
    /// <see cref="GlyphRenderResult.Premultiplied"/> matches a premultiplied-alpha SpriteBatch,
    /// <see cref="GlyphRenderResult.NonPremultiplied"/> matches AlphaBlend, and
    /// <see cref="GlyphRenderResult.NoAntialiasing"/> produces hard 1-bit edges for pixel-art fonts.
    /// </summary>
    public GlyphRenderResult GlyphRenderResult { get; set; } = GlyphRenderResult.Premultiplied;
}
