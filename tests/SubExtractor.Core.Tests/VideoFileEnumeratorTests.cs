using SubExtractor.Core.Scanning;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public class VideoFileEnumeratorTests
{
    [Fact]
    public void Enumerate_returns_only_top_level_video_files_in_name_order()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "b.MKV"), "");
        File.WriteAllText(Path.Combine(temp.Path, "a.mp4"), "");
        File.WriteAllText(Path.Combine(temp.Path, "notes.txt"), "");
        Directory.CreateDirectory(Path.Combine(temp.Path, "sub"));
        File.WriteAllText(Path.Combine(temp.Path, "sub", "c.mkv"), "");

        var files = VideoFileEnumerator.Enumerate(temp.Path);

        Assert.Equal(["a.mp4", "b.MKV"], files.Select(Path.GetFileName));
    }
}
