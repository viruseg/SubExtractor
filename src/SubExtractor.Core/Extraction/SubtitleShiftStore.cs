using Microsoft.Data.Sqlite;

namespace SubExtractor.Core.Extraction;

public sealed class SubtitleShiftStore
{
    private const string DatabaseFileName = "subtitle-shifts.db";

    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS subtitle_shifts (
            path_key TEXT PRIMARY KEY,
            path TEXT NOT NULL,
            ticks INTEGER NOT NULL
        )
        """;

    private readonly string _databasePath;
    private bool _schemaReady;

    public SubtitleShiftStore(string dataDirectory)
    {
        _databasePath = Path.Combine(dataDirectory, DatabaseFileName);
    }

    public TimeSpan Get(string folder)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ticks FROM subtitle_shifts WHERE path_key = $key";
        command.Parameters.AddWithValue("$key", BuildKey(folder));

        return command.ExecuteScalar() is long ticks ? TimeSpan.FromTicks(ticks) : TimeSpan.Zero;
    }

    public void Set(string folder, TimeSpan shift)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$key", BuildKey(folder));

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

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString());
        connection.Open();

        if (!_schemaReady)
        {
            using var command = connection.CreateCommand();
            command.CommandText = CreateSchemaSql;
            command.ExecuteNonQuery();
            _schemaReady = true;
        }

        return connection;
    }

    private static string BuildKey(string folder)
    {
        var fullPath = Path.GetFullPath(folder);
        var rootLength = Path.GetPathRoot(fullPath)?.Length ?? 0;
        var withoutTrailingSeparator = fullPath.Length > rootLength
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;

        return withoutTrailingSeparator.ToUpperInvariant();
    }
}
