using Microsoft.Data.Sqlite;

namespace SubExtractor.Core.Extraction;

internal sealed class SubtitleDatabase
{
    private const string DatabaseFileName = "subtitle-shifts.db";

    private const string CreateSchemaSql =
        """
        CREATE TABLE IF NOT EXISTS subtitle_shifts (
            path_key TEXT PRIMARY KEY,
            path TEXT NOT NULL,
            ticks INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS subtitle_text_removals (
            path_key TEXT PRIMARY KEY,
            path TEXT NOT NULL,
            rules TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS subtitle_global_text_removals (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            rules TEXT NOT NULL
        )
        """;

    private readonly string _databasePath;
    private bool _schemaReady;

    public SubtitleDatabase(string dataDirectory)
    {
        _databasePath = Path.Combine(dataDirectory, DatabaseFileName);
    }

    public SqliteConnection Open()
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

    public static string BuildKey(string folder)
    {
        var fullPath = Path.GetFullPath(folder);
        var rootLength = Path.GetPathRoot(fullPath)?.Length ?? 0;
        var withoutTrailingSeparator = fullPath.Length > rootLength
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;

        return withoutTrailingSeparator.ToUpperInvariant();
    }
}
