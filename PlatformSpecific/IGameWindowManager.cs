#if WINFORMS
using System;
using System.Windows.Forms;

#endif
namespace Rampastring.XNAUI.PlatformSpecific;

internal interface IGameWindowManager
{
#if WINFORMS
    event EventHandler GameWindowClosing;
    event EventHandler ClientSizeChanged;
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
    int GetWindowWidth();
    int GetWindowHeight();
    void SetFormBorderStyle(FormBorderStyle borderStyle);
#endif
    bool HasFocus();
    void CenterOnScreen();
    void SetBorderlessMode(bool value);

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
}