#if WINFORMS
using System;

namespace Rampastring.XNAUI.PlatformSpecific;

/// <summary>
/// Event arguments for files dropped onto the game window.
/// </summary>
public class FileDropEventArgs : EventArgs
{
    public FileDropEventArgs(string[] filePaths)
    {
        FilePaths = filePaths;
    }

    /// <summary>
    /// The full paths of the dropped files.
    /// </summary>
    public string[] FilePaths { get; }
}
#endif
