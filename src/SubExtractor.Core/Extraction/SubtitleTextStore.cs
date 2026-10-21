namespace SubExtractor.Core.Extraction;

public sealed class SubtitleTextStore
{
    private readonly SubtitleDatabase _database;

    public SubtitleTextStore(string dataDirectory)
    {
        _database = new SubtitleDatabase(dataDirectory);
    }

    public IReadOnlyList<string> GetFolderRules(string folder)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT rules FROM subtitle_text_removals WHERE path_key = $key";
        command.Parameters.AddWithValue("$key", SubtitleDatabase.BuildKey(folder));

        return command.ExecuteScalar() is string rules ? Split(rules) : [];
    }

    public void SetFolderRules(string folder, IReadOnlyList<string> rules)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$key", SubtitleDatabase.BuildKey(folder));

        if (rules.Count == 0)
        {
            command.CommandText = "DELETE FROM subtitle_text_removals WHERE path_key = $key";
        }
        else
        {
            command.CommandText =
                """
                INSERT INTO subtitle_text_removals (path_key, path, rules)
                VALUES ($key, $path, $rules)
                ON CONFLICT(path_key) DO UPDATE SET
                    path = excluded.path,
                    rules = excluded.rules
                """;
            command.Parameters.AddWithValue("$path", Path.GetFullPath(folder));
            command.Parameters.AddWithValue("$rules", string.Join("\n", rules));
        }

        command.ExecuteNonQuery();
    }

    public IReadOnlyList<string> GetGlobalRules()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT rules FROM subtitle_global_text_removals WHERE id = 1";

        return command.ExecuteScalar() is string rules ? Split(rules) : [];
    }

    public void SetGlobalRules(IReadOnlyList<string> rules)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();

        if (rules.Count == 0)
        {
            command.CommandText = "DELETE FROM subtitle_global_text_removals WHERE id = 1";
        }
        else
        {
            command.CommandText =
                """
                INSERT INTO subtitle_global_text_removals (id, rules)
                VALUES (1, $rules)
                ON CONFLICT(id) DO UPDATE SET rules = excluded.rules
                """;
            command.Parameters.AddWithValue("$rules", string.Join("\n", rules));
        }

        command.ExecuteNonQuery();
    }

    private static IReadOnlyList<string> Split(string rules) =>
        rules.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
}
