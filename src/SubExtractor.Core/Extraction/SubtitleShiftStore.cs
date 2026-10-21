namespace SubExtractor.Core.Extraction;

public sealed class SubtitleShiftStore
{
    private readonly SubtitleDatabase _database;

    public SubtitleShiftStore(string dataDirectory)
    {
        _database = new SubtitleDatabase(dataDirectory);
    }

    public TimeSpan Get(string folder)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ticks FROM subtitle_shifts WHERE path_key = $key";
        command.Parameters.AddWithValue("$key", SubtitleDatabase.BuildKey(folder));

        return command.ExecuteScalar() is long ticks ? TimeSpan.FromTicks(ticks) : TimeSpan.Zero;
    }

    public void Set(string folder, TimeSpan shift)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$key", SubtitleDatabase.BuildKey(folder));

        if (shift == TimeSpan.Zero)
        {
            command.CommandText = "DELETE FROM subtitle_shifts WHERE path_key = $key";
        }
        else
        {
            command.CommandText =
                """
                INSERT INTO subtitle_shifts (path_key, path, ticks)
                VALUES ($key, $path, $ticks)
                ON CONFLICT(path_key) DO UPDATE SET
                    path = excluded.path,
                    ticks = excluded.ticks
                """;
            command.Parameters.AddWithValue("$path", Path.GetFullPath(folder));
            command.Parameters.AddWithValue("$ticks", shift.Ticks);
        }

        command.ExecuteNonQuery();
    }
}
