namespace SubExtractor.Core.Scanning;

public interface IScanProgressSink
{
    void FileStarted(string filePath, int completedFiles, int totalFiles);

    void FileFinished(string filePath, int completedFiles, int totalFiles, int trackCount);

    void FileFailed(string filePath, int completedFiles, int totalFiles, string error);

    void FolderFailed(string error);
}
