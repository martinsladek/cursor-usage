using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CursorUsage;

static class CursorAuth
{
    private const string SafeStoragePrefix = "cursor-safe-storage:v1:";
    private static readonly string TempDb = Path.Combine(Path.GetTempPath(), "CursorUsage-state.vscdb");

    /// <summary>Clean up leaked temp files from older versions that used a GUID per read.</summary>
    public static void CleanupLegacyTempFiles()
    {
        try
        {
            foreach (string file in Directory.GetFiles(Path.GetTempPath(), "CursorUsage-*.vscdb*"))
            {
                if (string.Equals(file, TempDb, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(file, TempDb + "-wal", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(file, TempDb + "-shm", StringComparison.OrdinalIgnoreCase))
                    continue;
                TryDelete(file);
            }
        }
        catch { }
    }

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

        string temp = TempDb;
        try
        {
            File.Copy(db, temp, overwrite: true);
            string wal = db + "-wal";
            if (File.Exists(wal))
            {
                try { File.Copy(wal, temp + "-wal", overwrite: true); }
                catch { }
            }

            using var connection = new SqliteConnection($"Data Source={temp};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM ItemTable WHERE key = $key LIMIT 1";
            command.Parameters.AddWithValue("$key", "cursorAuth/accessToken");
            object? result = command.ExecuteScalar();
            string? stored = result as string;
            if (string.IsNullOrWhiteSpace(stored))
                return null;

            return UnwrapAccessToken(stored);
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

    /// <summary>
    /// Cursor may store a plaintext JWT or an Electron safeStorage blob
    /// (<c>cursor-safe-storage:v1:</c> + base64 of Chromium <c>v10</c> AES-256-GCM).
    /// </summary>
    private static string? UnwrapAccessToken(string stored)
    {
        if (!stored.StartsWith(SafeStoragePrefix, StringComparison.Ordinal))
            return stored;

        try
        {
            byte[]? masterKey = TryReadOsCryptKey();
            if (masterKey is null)
                return null;

            byte[] raw = Convert.FromBase64String(stored[SafeStoragePrefix.Length..]);
            // Chromium OSCrypt Windows: "v10" + 12-byte nonce + ciphertext + 16-byte tag
            const int prefixLen = 3;
            const int nonceLen = 12;
            const int tagLen = 16;
            if (raw.Length < prefixLen + nonceLen + tagLen)
                return null;
            if (raw[0] != (byte)'v' || raw[1] != (byte)'1' || raw[2] != (byte)'0')
                return null;

            ReadOnlySpan<byte> nonce = raw.AsSpan(prefixLen, nonceLen);
            ReadOnlySpan<byte> tag = raw.AsSpan(raw.Length - tagLen, tagLen);
            ReadOnlySpan<byte> ciphertext = raw.AsSpan(prefixLen + nonceLen, raw.Length - prefixLen - nonceLen - tagLen);
            byte[] plain = new byte[ciphertext.Length];
            using var gcm = new AesGcm(masterKey, tagLen);
            gcm.Decrypt(nonce, ciphertext, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? TryReadOsCryptKey()
    {
        string localState = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cursor",
            "Local State");

        if (!File.Exists(localState))
            return null;

        using var doc = JsonDocument.Parse(File.ReadAllText(localState));
        if (!doc.RootElement.TryGetProperty("os_crypt", out JsonElement osCrypt)
            || !osCrypt.TryGetProperty("encrypted_key", out JsonElement keyEl))
            return null;

        string? b64 = keyEl.GetString();
        if (string.IsNullOrWhiteSpace(b64))
            return null;

        byte[] encryptedKey = Convert.FromBase64String(b64);
        // Prefix "DPAPI" then CryptUnprotectData payload
        if (encryptedKey.Length < 5
            || encryptedKey[0] != (byte)'D'
            || encryptedKey[1] != (byte)'P'
            || encryptedKey[2] != (byte)'A'
            || encryptedKey[3] != (byte)'P'
            || encryptedKey[4] != (byte)'I')
            return null;

        return ProtectedData.Unprotect(
            encryptedKey.AsSpan(5).ToArray(),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
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
