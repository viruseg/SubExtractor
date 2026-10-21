using SubExtractor.Core.Extraction;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public sealed class SubtitleTextStoreTests
{
    [Fact]
    public void Saved_folder_rules_survive_a_new_store_instance()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        string[] rules = ["@ субтитров", "#ad", "Full line"];

        new SubtitleTextStore(dataDirectory).SetFolderRules(folder, rules);

        Assert.Equal(rules, new SubtitleTextStore(dataDirectory).GetFolderRules(folder));
    }

    [Fact]
    public void Unknown_folder_has_no_rules()
    {
        using var temp = new TempDirectory();
        var store = new SubtitleTextStore(Path.Combine(temp.Path, "Data"));

        Assert.Empty(store.GetFolderRules(Path.Combine(temp.Path, "Films")));
    }

    [Fact]
    public void Empty_folder_rules_remove_the_saved_entry()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var store = new SubtitleTextStore(dataDirectory);

        store.SetFolderRules(folder, ["Ad"]);
        store.SetFolderRules(folder, []);

        Assert.Empty(new SubtitleTextStore(dataDirectory).GetFolderRules(folder));
    }

    [Theory]
    [InlineData(@"Films")]
    [InlineData(@"FILMS")]
    [InlineData(@"Films\")]
    public void Folder_spelling_does_not_create_a_second_entry(string spelling)
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        string[] rules = ["Ad"];

        new SubtitleTextStore(dataDirectory).SetFolderRules(folder, rules);

        Assert.Equal(
            rules,
            new SubtitleTextStore(dataDirectory).GetFolderRules(Path.Combine(temp.Path, spelling)));
    }

    [Fact]
    public void Folders_keep_independent_rules()
    {
        using var temp = new TempDirectory();
        var store = new SubtitleTextStore(Path.Combine(temp.Path, "Data"));
        var first = Path.Combine(temp.Path, "First");
        var second = Path.Combine(temp.Path, "Second");

        store.SetFolderRules(first, ["First rule"]);
        store.SetFolderRules(second, ["Second rule"]);

        Assert.Equal(["First rule"], store.GetFolderRules(first));
        Assert.Equal(["Second rule"], store.GetFolderRules(second));
    }

    [Fact]
    public void Saved_global_rules_survive_a_new_store_instance()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        string[] rules = ["Global ad", "@watermark", "##literal hash"];

        new SubtitleTextStore(dataDirectory).SetGlobalRules(rules);

        Assert.Equal(rules, new SubtitleTextStore(dataDirectory).GetGlobalRules());
    }

    [Fact]
    public void Empty_global_rules_remove_the_saved_entry()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var store = new SubtitleTextStore(dataDirectory);

        store.SetGlobalRules(["Ad"]);
        store.SetGlobalRules([]);

        Assert.Empty(new SubtitleTextStore(dataDirectory).GetGlobalRules());
    }

    [Fact]
    public void Folder_and_global_rules_are_independent()
    {
        using var temp = new TempDirectory();
        var store = new SubtitleTextStore(Path.Combine(temp.Path, "Data"));
        var folder = Path.Combine(temp.Path, "Films");

        store.SetFolderRules(folder, ["Folder rule"]);
        store.SetGlobalRules(["Global rule"]);
        store.SetFolderRules(folder, []);

        Assert.Equal(["Global rule"], store.GetGlobalRules());

        store.SetFolderRules(folder, ["Folder rule"]);
        store.SetGlobalRules([]);

        Assert.Equal(["Folder rule"], store.GetFolderRules(folder));
    }
}
