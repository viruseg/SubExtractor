using RecycleBinToolkit;
using SubExtractor.Core.Extraction;

namespace SubExtractor.Services;

public sealed class RecycleBinFileRecycler : IFileRecycler
{
    public async Task<FileRecycleResult> RecycleAsync(
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
}
