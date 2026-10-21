using SubExtractor.Core.Extraction;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public sealed class TextRemovalSettingsTests
{
    [Fact]
    public void New_settings_load_global_rules()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        string[] rules = ["Global rule", "@watermark"];
        new SubtitleTextStore(dataDirectory).SetGlobalRules(rules);

        var settings = new TextRemovalSettings(new SubtitleTextStore(dataDirectory));

        Assert.Equal(rules, settings.GlobalRules);
    }

    [Fact]
    public void SelectFolder_loads_that_folder_rules()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var first = Path.Combine(temp.Path, "First");
        var second = Path.Combine(temp.Path, "Second");
        var store = new SubtitleTextStore(dataDirectory);
        store.SetFolderRules(first, ["First rule"]);
        store.SetFolderRules(second, ["Second rule"]);
        var settings = new TextRemovalSettings(new SubtitleTextStore(dataDirectory));

        settings.SelectFolder(first);

        Assert.Equal(first, settings.Folder);
        Assert.Equal(["First rule"], settings.FolderRules);

        settings.SelectFolder(second);

        Assert.Equal(["Second rule"], settings.FolderRules);

        settings.SelectFolder(null);

        Assert.Null(settings.Folder);
        Assert.Empty(settings.FolderRules);
    }

    [Fact]
    public void SelectFolder_raises_changed()
    {
        using var temp = new TempDirectory();
        var settings = new TextRemovalSettings(
            new SubtitleTextStore(Path.Combine(temp.Path, "Data")));
        var changes = 0;
        settings.Changed += (_, _) => changes++;

        settings.SelectFolder(Path.Combine(temp.Path, "Films"));

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Effective_rules_combine_folder_and_global_rules()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var store = new SubtitleTextStore(dataDirectory);
        store.SetFolderRules(folder, ["Folder rule"]);
        store.SetGlobalRules(["Global rule"]);
        var settings = new TextRemovalSettings(new SubtitleTextStore(dataDirectory));

        settings.SelectFolder(folder);

        Assert.Equal(["Folder rule", "Global rule"], settings.EffectiveRules);
    }

    [Fact]
    public void UpdateFolderRules_is_not_persisted_before_save()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var settings = new TextRemovalSettings(new SubtitleTextStore(dataDirectory));
        settings.SelectFolder(folder);

        settings.UpdateFolderRules(["New rule"]);

        Assert.Empty(new SubtitleTextStore(dataDirectory).GetFolderRules(folder));
    }

    [Fact]
    public void UpdateGlobalRules_is_not_persisted_before_save()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var settings = new TextRemovalSettings(new SubtitleTextStore(dataDirectory));

        settings.UpdateGlobalRules(["New rule"]);

        Assert.Empty(new SubtitleTextStore(dataDirectory).GetGlobalRules());
    }

    [Fact]
    public void Save_persists_folder_and_global_rules()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var settings = new TextRemovalSettings(new SubtitleTextStore(dataDirectory));
        settings.SelectFolder(folder);
        settings.UpdateFolderRules(["Folder rule"]);
        settings.UpdateGlobalRules(["Global rule"]);

        settings.Save();

        Assert.Equal(
            ["Folder rule"],
            new SubtitleTextStore(dataDirectory).GetFolderRules(folder));
        Assert.Equal(
            ["Global rule"],
            new SubtitleTextStore(dataDirectory).GetGlobalRules());
    }

    [Fact]
    public void Switching_folders_saves_unsaved_folder_rules()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var first = Path.Combine(temp.Path, "First");
        var second = Path.Combine(temp.Path, "Second");
        var settings = new TextRemovalSettings(new SubtitleTextStore(dataDirectory));
        settings.SelectFolder(first);
        settings.UpdateFolderRules(["Edited rule"]);

        settings.SelectFolder(second);

        Assert.Equal(
            ["Edited rule"],
            new SubtitleTextStore(dataDirectory).GetFolderRules(first));
    }

    [Fact]
    public void Clearing_rules_persists_an_empty_list()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var store = new SubtitleTextStore(dataDirectory);
        store.SetFolderRules(folder, ["Old rule"]);
        store.SetGlobalRules(["Old global"]);
        var settings = new TextRemovalSettings(new SubtitleTextStore(dataDirectory));
        settings.SelectFolder(folder);
        settings.UpdateFolderRules([]);
        settings.UpdateGlobalRules([]);

        settings.Save();

        Assert.Empty(new SubtitleTextStore(dataDirectory).GetFolderRules(folder));
        Assert.Empty(new SubtitleTextStore(dataDirectory).GetGlobalRules());
    }

    [Fact]
    public void Whitespace_only_rules_are_not_stored()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var settings = new TextRemovalSettings(new SubtitleTextStore(dataDirectory));

        settings.UpdateGlobalRules(["", "   ", "@ ", "Rule"]);
        settings.Save();

        Assert.Equal(["@ ", "Rule"], new SubtitleTextStore(dataDirectory).GetGlobalRules());
    }

    [Fact]
    public void Updating_folder_rules_without_a_folder_is_ignored()
    {
        using var temp = new TempDirectory();
        var settings = new TextRemovalSettings(
            new SubtitleTextStore(Path.Combine(temp.Path, "Data")));

        settings.UpdateFolderRules(["Rule"]);

        Assert.Empty(settings.FolderRules);
        Assert.Empty(settings.EffectiveRules);
    }

    [Fact]
    public void Update_raises_changed()
    {
        using var temp = new TempDirectory();
        var settings = new TextRemovalSettings(
            new SubtitleTextStore(Path.Combine(temp.Path, "Data")));
        var changes = 0;
        settings.Changed += (_, _) => changes++;

        settings.UpdateGlobalRules(["Rule"]);

        Assert.Equal(1, changes);
    }
}
