using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace SubExtractor.Services;

public static class WindowOwner
{
    private const int OwnerWindowIndex = -8;

    public static void SetOwner(Window child, Window owner)
    {
        var childHandle = WinRT.Interop.WindowNative.GetWindowHandle(child);
        var ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        // WHY: WinUI windows have no owner property, so the Win32 owner relationship is set directly.
        SetWindowLongPtr(childHandle, OwnerWindowIndex, ownerHandle);
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);
}
