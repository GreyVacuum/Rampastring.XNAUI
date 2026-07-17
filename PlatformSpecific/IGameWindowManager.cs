using System;
#if WINFORMS
using System.Windows.Forms;

#endif
namespace Rampastring.XNAUI.PlatformSpecific;

internal interface IGameWindowManager
{
    event EventHandler ClientSizeChanged;

#if WINFORMS
    event EventHandler GameWindowClosing;
    event EventHandler<FileDropEventArgs> FilesDropped;

    void AllowClosing();
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows5.1.2600")]
#endif
    void FlashWindow();
    IntPtr GetWindowHandle();
    void HideWindow();
    void MaximizeWindow();
    void MinimizeWindow();
    void PreventClosing();
    void SetMaximizeBox(bool value);
    void SetControlBox(bool value);
    void SetIcon(string path);
    void ShowWindow();
    void SetFormBorderStyle(FormBorderStyle borderStyle);
#endif
    bool HasFocus();
    void CenterOnScreen();
    void SetBorderlessMode(bool value);

    /// <summary>
    /// Enables or disables user resizing of the game window.
    /// </summary>
    /// <param name="allow">True to allow resizing, false to disallow.</param>
    void EnableResizing(bool allow);

    /// <summary>
    /// Sets the minimum window size. The window cannot be resized smaller than this.
    /// </summary>
    /// <param name="width">Minimum width in pixels.</param>
    /// <param name="height">Minimum height in pixels.</param>
    void SetMinimumSize(int width, int height);

    int GetWindowWidth();
    int GetWindowHeight();

    /// <summary>
    /// Toggles between windowed and borderless fullscreen mode at runtime.
    /// Saves the current window state when entering fullscreen and restores it when leaving.
    /// </summary>
    void ToggleFullscreen();

    /// <summary>
    /// Gets a boolean that determines whether the window is currently in
    /// runtime-toggled borderless fullscreen mode.
    /// </summary>
    bool IsFullscreen { get; }

    /// <summary>
    /// Raised when the fullscreen state changes. Provides the new window dimensions.
    /// </summary>
    event EventHandler<FullscreenStateChangedEventArgs> FullscreenStateChanged;
}

/// <summary>
/// Arguments for the FullscreenStateChanged event.
/// </summary>
internal class FullscreenStateChangedEventArgs : EventArgs
{
    public bool IsFullscreen { get; }
    public int NewWidth { get; }
    public int NewHeight { get; }
    public bool AllowResizing { get; }

    public FullscreenStateChangedEventArgs(bool isFullscreen, int newWidth, int newHeight, bool allowResizing)
    {
        IsFullscreen = isFullscreen;
        NewWidth = newWidth;
        NewHeight = newHeight;
        AllowResizing = allowResizing;
    }
}