using SubExtractor.Core.Processing;

namespace SubExtractor.Core.Tests.TestHelpers;

public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Func<string, IReadOnlyList<string>, CancellationToken, ProcessResult> _handler;

    public FakeProcessRunner(ProcessResult result) : this((_, _, _) => result) { }

    public FakeProcessRunner(Func<string, IReadOnlyList<string>, CancellationToken, ProcessResult> handler)
        => _handler = handler;

    public string? LastExecutable { get; private set; }
    public IReadOnlyList<string>? LastArguments { get; private set; }

    public Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        LastExecutable = executablePath;
        LastArguments = arguments;
        return Task.FromResult(_handler(executablePath, arguments, cancellationToken));
    }
}
