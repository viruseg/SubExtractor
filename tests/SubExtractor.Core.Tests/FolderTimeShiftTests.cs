using SubExtractor.Core.Extraction;
using SubExtractor.Core.Tests.TestHelpers;

namespace SubExtractor.Core.Tests;

public sealed class FolderTimeShiftTests
{
    [Fact]
    public void Selecting_a_folder_loads_its_saved_shift()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        new SubtitleShiftStore(dataDirectory).Set(folder, TimeSpan.FromSeconds(3));
        var shifts = new FolderTimeShift(new SubtitleShiftStore(dataDirectory));

        shifts.SelectFolder(folder);

        Assert.Equal(folder, shifts.Folder);
        Assert.Equal(TimeSpan.FromSeconds(3), shifts.Shift);
    }

    [Fact]
    public void Selecting_a_folder_without_saved_shift_resets_to_zero()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var first = Path.Combine(temp.Path, "First");
        var second = Path.Combine(temp.Path, "Second");
        new SubtitleShiftStore(dataDirectory).Set(first, TimeSpan.FromSeconds(5));
        var shifts = new FolderTimeShift(new SubtitleShiftStore(dataDirectory));
        shifts.SelectFolder(first);

        shifts.SelectFolder(second);

        Assert.Equal(second, shifts.Folder);
        Assert.Equal(TimeSpan.Zero, shifts.Shift);
    }

    [Fact]
    public void Setting_shift_saves_it_for_the_selected_folder()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var shifts = new FolderTimeShift(new SubtitleShiftStore(dataDirectory));
        shifts.SelectFolder(folder);

        shifts.SetShift(TimeSpan.FromSeconds(7));
        shifts.SelectFolder(null);
        shifts.SelectFolder(folder);

        Assert.Equal(TimeSpan.FromSeconds(7), shifts.Shift);
    }

    [Fact]
    public void Setting_shift_without_a_selected_folder_is_ignored()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var shifts = new FolderTimeShift(new SubtitleShiftStore(dataDirectory));

        shifts.SetShift(TimeSpan.FromSeconds(5));
        shifts.SelectFolder(folder);

        Assert.Equal(TimeSpan.Zero, shifts.Shift);
    }

    [Fact]
    public void Setting_zero_removes_the_saved_shift()
    {
        using var temp = new TempDirectory();
        var dataDirectory = Path.Combine(temp.Path, "Data");
        var folder = Path.Combine(temp.Path, "Films");
        var shifts = new FolderTimeShift(new SubtitleShiftStore(dataDirectory));
        shifts.SelectFolder(folder);
        shifts.SetShift(TimeSpan.FromSeconds(4));

        shifts.SetShift(TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, new SubtitleShiftStore(dataDirectory).Get(folder));
    }

    [Fact]
    public void Selecting_a_folder_notifies_subscribers()
    {
        using var temp = new TempDirectory();
        var shifts = new FolderTimeShift(new SubtitleShiftStore(Path.Combine(temp.Path, "Data")));
        var notifications = 0;
        shifts.Changed += (_, _) => notifications++;

        shifts.SelectFolder(Path.Combine(temp.Path, "Films"));

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void Storage_failure_keeps_the_shift_in_memory_and_is_reported()
    {
        using var temp = new TempDirectory();
        var dataFile = Path.Combine(temp.Path, "Data");
        File.WriteAllText(dataFile, string.Empty);
        var shifts = new FolderTimeShift(new SubtitleShiftStore(dataFile));
        var failures = new List<Exception>();
        shifts.PersistenceFailed += (_, exception) => failures.Add(exception);

        shifts.SelectFolder(Path.Combine(temp.Path, "Films"));
        shifts.SetShift(TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(2), shifts.Shift);
        Assert.NotEmpty(failures);
    }
}
