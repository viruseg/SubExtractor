namespace SubExtractor.Core.Extraction;

public sealed class FolderTimeShift
{
    private readonly SubtitleShiftStore _store;

    public FolderTimeShift(SubtitleShiftStore store)
    {
        _store = store;
    }

    public event EventHandler? Changed;

    public event EventHandler<Exception>? PersistenceFailed;

    public string? Folder { get; private set; }

    public TimeSpan Shift { get; private set; }

    public void SelectFolder(string? folder)
    {
        Folder = folder;
        Shift = folder is null ? TimeSpan.Zero : LoadShift(folder);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetShift(TimeSpan shift)
    {
        if (Folder is not { } folder || shift == Shift)
        {
            return;
        }

        Shift = shift;

        try
        {
            _store.Set(folder, shift);
        }
        catch (Exception exception)
        {
            PersistenceFailed?.Invoke(this, exception);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private TimeSpan LoadShift(string folder)
    {
        try
        {
            return _store.Get(folder);
        }
        catch (Exception exception)
        {
            PersistenceFailed?.Invoke(this, exception);
            return TimeSpan.Zero;
        }
    }
}
