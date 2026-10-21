using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

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

    public static void CenterOnOwner(Window child, Window owner)
    {
        var ownerPosition = owner.AppWindow.Position;
        var ownerSize = owner.AppWindow.Size;
        var childSize = child.AppWindow.Size;
        var workArea = DisplayArea.GetFromWindowId(owner.AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;

        var x = ownerPosition.X + ((ownerSize.Width - childSize.Width) / 2);
        var y = ownerPosition.Y + ((ownerSize.Height - childSize.Height) / 2);

        child.AppWindow.Move(new PointInt32(
            Clamp(x, workArea.X, workArea.X + workArea.Width - childSize.Width),
            Clamp(y, workArea.Y, workArea.Y + workArea.Height - childSize.Height)));
    }

    private static int Clamp(int value, int min, int max) =>
        max < min ? min : Math.Clamp(value, min, max);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);
}
