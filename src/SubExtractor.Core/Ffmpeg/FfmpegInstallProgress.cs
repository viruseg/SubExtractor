namespace SubExtractor.Core.Ffmpeg;

public enum FfmpegInstallStage
{
    Downloading,
    Verifying,
    Extracting,
    Installing,
    Done,
}

public sealed record FfmpegInstallProgress(FfmpegInstallStage Stage, double? Percent);
