namespace SubExtractor.Core.Extraction;

public sealed class TextRemovalSettings
{
    private readonly SubtitleTextStore _store;
    private bool _folderDirty;
    private bool _globalDirty;

    public TextRemovalSettings(SubtitleTextStore store)
    {
        _store = store;
        GlobalRules = LoadGlobalRules();
    }

    public event EventHandler? Changed;

    public event EventHandler<Exception>? PersistenceFailed;

    public string? Folder { get; private set; }

    public IReadOnlyList<string> FolderRules { get; private set; } = [];

    public IReadOnlyList<string> GlobalRules { get; private set; }

    public IReadOnlyList<string> EffectiveRules => [.. FolderRules, .. GlobalRules];

    public void SelectFolder(string? folder)
    {
        SaveFolderRules();
        Folder = folder;
        FolderRules = folder is null ? [] : LoadFolderRules(folder);
        _folderDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateFolderRules(IReadOnlyList<string> rules)
    {
        if (Folder is null)
        {
            return;
        }

        var normalized = Normalize(rules);
        if (FolderRules.SequenceEqual(normalized))
        {
            return;
        }

        FolderRules = normalized;
        _folderDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateGlobalRules(IReadOnlyList<string> rules)
    {
        var normalized = Normalize(rules);
        if (GlobalRules.SequenceEqual(normalized))
        {
            return;
        }

        GlobalRules = normalized;
        _globalDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        SaveFolderRules();
        SaveGlobalRules();
    }

    private void SaveFolderRules()
    {
        if (!_folderDirty || Folder is not { } folder)
        {
            return;
        }

        try
        {
            _store.SetFolderRules(folder, FolderRules);
            _folderDirty = false;
        }
        catch (Exception exception)
        {
            PersistenceFailed?.Invoke(this, exception);
        }
    }

    private void SaveGlobalRules()
    {
        if (!_globalDirty)
        {
            return;
        }

        try
        {
            _store.SetGlobalRules(GlobalRules);
            _globalDirty = false;
        }
        catch (Exception exception)
        {
            PersistenceFailed?.Invoke(this, exception);
        }
    }

    private IReadOnlyList<string> LoadFolderRules(string folder)
    {
        try
        {
            return _store.GetFolderRules(folder);
        }
        catch (Exception exception)
        {
            PersistenceFailed?.Invoke(this, exception);
            return [];
        }
    }

    private IReadOnlyList<string> LoadGlobalRules()
    {
        try
        {
            return _store.GetGlobalRules();
        }
        catch (Exception exception)
        {
            PersistenceFailed?.Invoke(this, exception);
            return [];
        }
    }

    private static string[] Normalize(IReadOnlyList<string> rules) =>
        rules.Where(rule => !string.IsNullOrWhiteSpace(rule)).ToArray();
}
