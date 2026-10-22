using System.Diagnostics;
using SubExtractor.Core.Processing;

namespace SubExtractor.Core.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_cancellation_throws_operation_canceled_and_terminates_process()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var processName = $"SrTest{suffix}";
        var executablePath = Path.Combine(Path.GetTempPath(), $"{processName}.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);

        using var cancellation = new CancellationTokenSource();
        Task<ProcessResult>? run = null;

        try
        {
            var runner = new ProcessRunner();
            run = runner.RunAsync(
                executablePath,
                new[] { "-n", "60", "127.0.0.1" },
                _ => { },
                cancellation.Token);

            Assert.True(
                await WaitForProcessStateAsync(processName, running: true),
                "The test process did not start.");

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

            Assert.True(
                await WaitForProcessStateAsync(processName, running: false),
                "The canceled process was not terminated.");
        }
        finally
        {
            cancellation.Cancel();
            if (run is not null)
            {
                try { await run; } catch { }
            }

            KillProcesses(processName);

            try { File.Delete(executablePath); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task RunAsync_streams_standard_output_lines_before_the_process_exits()
    {
        var lines = new List<string>();
        var firstLine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ProcessRunner();

        var run = runner.RunAsync(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            ["/c", "echo first & ping -n 3 127.0.0.1 >nul & echo second"],
            line =>
            {
                lock (lines)
                {
                    lines.Add(line);
                }

                if (line.Trim() == "first")
                {
                    firstLine.TrySetResult();
                }
            },
            CancellationToken.None);

        await firstLine.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(run.IsCompleted);

        var result = await run;

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(lines, line => line.Trim() == "first");
        Assert.Contains(lines, line => line.Trim() == "second");
        Assert.Contains("first", result.StandardOutput);
        Assert.Contains("second", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_throwing_callback_does_not_stall_the_process()
    {
        using var cancellation = new CancellationTokenSource();
        var run = new ProcessRunner().RunAsync(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            ["/c", "for /L %i in (1,1,20000) do @echo 0123456789012345678901234567890123456789"],
            _ => throw new InvalidOperationException("callback boom"),
            cancellation.Token);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => run.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            cancellation.Cancel();
            try { await run; } catch { }
        }
    }

    private static async Task<bool> WaitForProcessStateAsync(string processName, bool running)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (IsProcessRunning(processName) == running)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private static bool IsProcessRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static void KillProcesses(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
