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
