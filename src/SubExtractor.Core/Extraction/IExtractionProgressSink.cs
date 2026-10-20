namespace SubExtractor.Core.Extraction;

public interface IExtractionProgressSink
{
    void FileStarted(string videoPath, int completedOverall, int totalOverall);

    void TrackFinished(
        string videoPath,
        int completedInFile,
        int totalInFile,
        int completedOverall,
        int totalOverall);

    void TrackFailed(
        string videoPath,
        int completedInFile,
        int totalInFile,
        int completedOverall,
        int totalOverall,
        string error);

    void FileFinished(string videoPath);
}
