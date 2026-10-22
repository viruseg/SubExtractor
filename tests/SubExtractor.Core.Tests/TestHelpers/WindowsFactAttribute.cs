using Xunit;

namespace SubExtractor.Core.Tests.TestHelpers;

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Requires a Windows PE file.";
        }
    }
}
