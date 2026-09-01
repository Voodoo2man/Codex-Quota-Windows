using System.Runtime.InteropServices;

namespace CodexQuota;

internal static class Native
{
    public const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;
    [DllImport("user32.dll", SetLastError = true)] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { public int State; public int Flags; public int GradientColor; public int AnimationId; }
    [StructLayout(LayoutKind.Sequential)] private struct CompositionData { public int Attribute; public IntPtr Data; public int Size; }
    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref CompositionData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyWindows11Frame(System.Windows.Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var darkMode = 1;
        var roundedCorners = 2;
        DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));
        DwmSetWindowAttribute(hwnd, 33, ref roundedCorners, sizeof(int));
    }

    public static void EnableAcrylic(IntPtr hwnd)
    {
        var accent = new AccentPolicy { State = 4, Flags = 2, GradientColor = unchecked((int)0xB8202733) };
        var size = Marshal.SizeOf<AccentPolicy>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new CompositionData { Attribute = 19, Data = ptr, Size = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }
}
