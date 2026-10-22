using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;

namespace SubExtractor.Core.Processing;

public sealed class ProcessRunner : IProcessRunner
{
    public Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        RunAsync(executablePath, arguments, onStandardOutputLine: null, cancellationToken);

    public async Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        Action<string>? onStandardOutputLine,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutput = ReadStandardOutputAsync(
            process.StandardOutput,
            onStandardOutputLine,
            cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);

            return new ProcessResult(
                process.ExitCode,
                standardOutput.Result,
                standardError.Result);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);

            try
            {
                await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            }
            catch
            {
            }

            throw;
        }
    }

    private static async Task<string> ReadStandardOutputAsync(
        StreamReader reader,
        Action<string>? onStandardOutputLine,
        CancellationToken cancellationToken)
    {
        if (onStandardOutputLine is null)
        {
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        var output = new StringBuilder();
        Exception? callbackException = null;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            output.AppendLine(line);

            if (callbackException is not null)
            {
                continue;
            }

            try
            {
                onStandardOutputLine(line);
            }
            catch (Exception ex)
            {
                callbackException = ex;
            }
        }

        if (callbackException is not null)
        {
            ExceptionDispatchInfo.Capture(callbackException).Throw();
        }

        return output.ToString();
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // NOTE: Best-effort kill; cancellation must still surface as OperationCanceledException.
        }
    }
}
