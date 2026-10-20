namespace SubExtractor.Services;

public sealed class AppBusyState
{
    private readonly object _gate = new();
    private int _count;
    private int _updateCount;

    public event EventHandler? Changed;

    public bool IsBusy
    {
        get
        {
            lock (_gate)
            {
                return _count > 0;
            }
        }
    }

    public bool IsUpdate
    {
        get
        {
            lock (_gate)
            {
                return _updateCount > 0;
            }
        }
    }

    public IDisposable Begin(bool isUpdate = false)
    {
        lock (_gate)
        {
            _count++;
            if (isUpdate)
            {
                _updateCount++;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return new Scope(this, isUpdate);
    }

    private void End(bool isUpdate)
    {
        lock (_gate)
        {
            if (_count == 0)
            {
                return;
            }

            _count--;
            if (isUpdate && _updateCount > 0)
            {
                _updateCount--;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Scope(AppBusyState owner, bool isUpdate) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            owner.End(isUpdate);
        }
    }
}
