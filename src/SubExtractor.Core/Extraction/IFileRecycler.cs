namespace SubExtractor.Core.Extraction;

public interface IFileRecycler
{
    Task<FileRecycleResult> RecycleAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken);
}
