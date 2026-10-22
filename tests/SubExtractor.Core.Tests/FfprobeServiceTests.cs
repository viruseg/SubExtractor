using SubExtractor.Core.Ffprobe;
using SubExtractor.Core.Processing;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public class FfprobeServiceTests
{
    [Fact]
    public async Task Probe_builds_expected_arguments_and_parses_output()
    {
        const string json = """{"streams":[{"index":2,"codec_name":"subrip","tags":{"language":"eng"}}]}""";
        var runner = new FakeProcessRunner(new ProcessResult(0, json, ""));
        var service = new FfprobeService(runner, @"C:\Data\ffmpeg\ffprobe.exe");

        var tracks = await service.ProbeAsync(@"C:\v\Movie.mkv", CancellationToken.None);

        Assert.Single(tracks);
        Assert.Equal("ffprobe.exe", Path.GetFileName(runner.LastExecutable));
        Assert.Contains("-select_streams", runner.LastArguments!);
        Assert.Contains("-of", runner.LastArguments!);
        Assert.Contains(runner.LastArguments!, argument => argument.Contains("format=duration"));
        Assert.Contains(@"C:\v\Movie.mkv", runner.LastArguments!);
    }

    [Fact]
    public async Task Probe_throws_with_stderr_on_failure()
    {
        var runner = new FakeProcessRunner(new ProcessResult(1, "", "boom"));
        var service = new FfprobeService(runner, "ffprobe.exe");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ProbeAsync("x.mkv", CancellationToken.None));

        Assert.Contains("boom", ex.Message);
    }
}
