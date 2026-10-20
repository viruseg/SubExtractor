namespace SubExtractor.Core.Processing;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
