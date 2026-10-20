using Microsoft.UI.Xaml;

namespace SubExtractor.Services;

public sealed class OperationCoordinator
{
    private readonly object _gate = new();
    private CancellationTokenSource? _source;
    private bool _exitRequested;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _source is not null;
            }
        }
    }

    public bool ExitRequested
    {
        get
        {
            lock (_gate)
            {
                return _exitRequested;
            }
        }
    }

    public OperationScope Begin()
    {
        lock (_gate)
        {
            if (_source is not null)
            {
                throw new InvalidOperationException("Another operation is already running.");
            }

            _source = new CancellationTokenSource();
            return new OperationScope(this, _source);
        }
    }

    public void RequestExit()
    {
        CancellationTokenSource? source;
        lock (_gate)
        {
            _exitRequested = true;
            source = _source;
        }

        source?.Cancel();
    }

    private void Complete(CancellationTokenSource source)
    {
        bool exit;
        lock (_gate)
        {
            if (!ReferenceEquals(_source, source))
            {
                return;
            }

            _source = null;
            exit = _exitRequested;
        }

        source.Dispose();

        if (exit)
        {
            Application.Current.Exit();
        }
    }

    public sealed class OperationScope : IDisposable
    {
        private readonly OperationCoordinator _owner;
        private readonly CancellationTokenSource _source;
        private int _disposed;

        internal OperationScope(OperationCoordinator owner, CancellationTokenSource source)
        {
            _owner = owner;
            _source = source;
        }

        public CancellationToken Token => _source.Token;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _owner.Complete(_source);
        }
    }
}
