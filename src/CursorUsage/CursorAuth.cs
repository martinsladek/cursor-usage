using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CursorUsage;

static class CursorAuth
{
    public static string? TryReadSessionCookie()
    {
        string? token = TryReadAccessToken();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (!TryReadJwtPayload(token, out JsonElement payload))
            return null;

        if (payload.TryGetProperty("exp", out JsonElement expEl)
            && expEl.TryGetInt64(out long exp)
            && DateTimeOffset.FromUnixTimeSeconds(exp) <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        if (!payload.TryGetProperty("sub", out JsonElement subEl))
            return null;

        string? sub = subEl.GetString();
        if (string.IsNullOrWhiteSpace(sub))
            return null;

        return $"{sub}%3A%3A{token}";
    }

    private static string? TryReadAccessToken()
    {
        string db = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cursor",
            "User",
            "globalStorage",
            "state.vscdb");

        if (!File.Exists(db))
            return null;

        string temp = Path.Combine(Path.GetTempPath(), "CursorUsage-" + Guid.NewGuid().ToString("N") + ".vscdb");
        try
        {
            File.Copy(db, temp, overwrite: true);
            string wal = db + "-wal";
            if (File.Exists(wal))
            {
                try { File.Copy(wal, temp + "-wal", overwrite: true); }
                catch { }
            }

            using var connection = new SqliteConnection($"Data Source={temp};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM ItemTable WHERE key = $key LIMIT 1";
            command.Parameters.AddWithValue("$key", "cursorAuth/accessToken");
            object? result = command.ExecuteScalar();
            return result as string;
        }
        catch
        {
            return null;
        }
        finally
        {
            TryDelete(temp);
            TryDelete(temp + "-wal");
            TryDelete(temp + "-shm");
        }
    }

    private static bool TryReadJwtPayload(string jwt, out JsonElement payload)
    {
        payload = default;
        string[] parts = jwt.Split('.');
        if (parts.Length < 2)
            return false;

        try
        {
            byte[] json = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(json);
            payload = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
