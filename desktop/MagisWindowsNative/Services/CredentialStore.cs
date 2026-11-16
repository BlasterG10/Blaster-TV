using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MagisWindowsNative.Services;

public sealed class SavedCredentials
{
    public string Server { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public static class CredentialStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MagisWindowsNative");
    private static readonly string FilePath = Path.Combine(Folder, "login.dat");

    // DPAPI CurrentUser: only the same Windows user profile can decrypt this file.
    public static void Save(SavedCredentials credentials)
    {
        Directory.CreateDirectory(Folder);
        var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(credentials));
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, encrypted);
        CryptographicOperations.ZeroMemory(plain);
    }

    public static SavedCredentials? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            var encrypted = File.ReadAllBytes(FilePath);
            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            try { return JsonSerializer.Deserialize<SavedCredentials>(plain); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch
        {
            // Corrupt or copied-to-another-Windows-profile credentials are not reused.
            return null;
        }
    }

    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
