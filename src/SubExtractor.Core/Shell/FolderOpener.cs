using System.ComponentModel;
using System.Diagnostics;

namespace SubExtractor.Core.Shell;

public sealed record FolderOpenResult(bool Opened, string? ErrorMessage);

public sealed class FolderOpener
{
    public static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public FolderOpenResult Open(string path)
    {
        try
        {
            var target = NormalizePath(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{target}\"",
                UseShellExecute = true,
            });

            return new FolderOpenResult(true, null);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException
            or ArgumentException or NotSupportedException)
        {
            return new FolderOpenResult(false, $"Couldn't open the folder: {exception.Message}");
        }
    }
}
