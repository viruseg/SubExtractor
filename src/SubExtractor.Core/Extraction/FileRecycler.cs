using System.Runtime.Versioning;
using RecycleBinToolkit;

namespace SubExtractor.Core.Extraction;

public sealed class FileRecycler : IFileRecycler
{
    public async Task<FileRecycleResult> RecycleAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
        {
            return await RecycleToBinAsync(paths, cancellationToken).ConfigureAwait(false);
        }

        return DeletePermanently(paths, cancellationToken);
    }

    [SupportedOSPlatform("windows10.0.14393")]
    private static async Task<FileRecycleResult> RecycleToBinAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var result = await RecycleBin
            .RecycleAsync(paths, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new FileRecycleResult(
            result.Failures
                .Select(failure => new FileRecycleFailure(
                    failure.Path,
                    failure.Stage.ToString(),
                    failure.HResult,
                    failure.Attempts))
                .ToList());
    }

    private static FileRecycleResult DeletePermanently(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var failures = new List<FileRecycleFailure>();

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                failures.Add(new FileRecycleFailure(path, "PermanentDeleteFailed", ex.HResult, 1));
            }
        }

        return new FileRecycleResult(failures);
    }
}
