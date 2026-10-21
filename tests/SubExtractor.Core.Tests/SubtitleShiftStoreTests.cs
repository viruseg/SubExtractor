using SubExtractor.Core.Extraction;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public sealed class SubtitleShiftStoreTests
{
    [Fact]
    public void Saved_shift_survives_a_new_store_instance()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var shift = TimeSpan.FromMilliseconds(1500);

        new SubtitleShiftStore(dataDirectory).Set(folder, shift);

        Assert.Equal(shift, new SubtitleShiftStore(dataDirectory).Get(folder));
    }

    [Fact]
    public void Unknown_folder_has_no_shift()
    {
        using var temp = new TempDirectory();
        var store = new SubtitleShiftStore(Path.Combine(temp.Path, "Data"));

        Assert.Equal(TimeSpan.Zero, store.Get(Path.Combine(temp.Path, "Films")));
    }

    [Fact]
    public void Zero_shift_removes_the_saved_entry()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var store = new SubtitleShiftStore(dataDirectory);

        store.Set(folder, TimeSpan.FromSeconds(2));
        store.Set(folder, TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, new SubtitleShiftStore(dataDirectory).Get(folder));
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
        var shift = TimeSpan.FromSeconds(4);

        new SubtitleShiftStore(dataDirectory).Set(folder, shift);

        Assert.Equal(shift, new SubtitleShiftStore(dataDirectory).Get(Path.Combine(temp.Path, spelling)));
    }

    [Fact]
    public void Folders_keep_independent_shifts()
    {
        using var temp = new TempDirectory();
        var store = new SubtitleShiftStore(Path.Combine(temp.Path, "Data"));
        var first = Path.Combine(temp.Path, "First");
        var second = Path.Combine(temp.Path, "Second");

        store.Set(first, TimeSpan.FromSeconds(1));
        store.Set(second, TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(1), store.Get(first));
        Assert.Equal(TimeSpan.FromSeconds(2), store.Get(second));
    }
}
