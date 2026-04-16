using Microsoft.Extensions.Configuration;

namespace SellBotLk.Api;

/// <summary>
/// Loads a local .env file (optional) and maps common flat env var names to
/// the nested keys this API reads via IConfiguration (e.g. Gemini:ApiKey).
/// </summary>
internal static class LegacyEnvConfiguration
{
    /// <summary>
    /// Loads <c>.env</c> from the API project directory (walking up from
    /// <see cref="AppContext.BaseDirectory"/> until a <c>*.csproj</c> is found), then
    /// from the process current directory, so <c>dotnet run</c> works from the repo
    /// root or the project folder.
    /// </summary>
    public static void LoadLocalDotEnv()
    {
        foreach (var path in GetDotEnvPaths())
        {
            if (File.Exists(path))
                DotNetEnv.Env.Load(path);
        }
    }

    private static IEnumerable<string> GetDotEnvPaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in new[] { GetApiProjectDirectory(), Directory.GetCurrentDirectory() })
        {
            var p = Path.Combine(dir, ".env");
            var full = Path.GetFullPath(p);
            if (seen.Add(full))
                yield return full;
        }
    }

    /// <summary>Directory containing the API <c>.csproj</c> (not <c>bin</c>).</summary>
    private static string GetApiProjectDirectory()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && dir != null; i++)
            {
                if (dir.GetFiles("*.csproj").Length > 0)
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch
        {
            // fall through
        }

        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Fills configuration keys only when they are missing or whitespace, so
    /// appsettings.json, user secrets, or <c>Gemini__ApiKey</c>-style env vars still win.
    /// </summary>
    public static void ApplyFlatEnvAliases(ConfigurationManager configuration)
    {
        void SetFromEnv(string envKey, string configKey)
        {
            var current = configuration[configKey];
            if (!string.IsNullOrWhiteSpace(current)) return;

            var v = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(v))
                configuration[configKey] = v.Trim();
        }

        SetFromEnv("GEMINI_API_KEY", "Gemini:ApiKey");
        SetFromEnv("GEMINI_MODEL", "Gemini:Model");

        SetFromEnv("WHATSAPP_TOKEN", "WhatsApp:Token");
        SetFromEnv("WHATSAPP_VERIFY_TOKEN", "WhatsApp:VerifyToken");
        SetFromEnv("WHATSAPP_PHONE_NUMBER_ID", "WhatsApp:PhoneNumberId");
        SetFromEnv("WHATSAPP_APP_SECRET", "WhatsApp:AppSecret");
        SetFromEnv("META_APP_SECRET", "WhatsApp:AppSecret");

        SetFromEnv("DB_CONNECTION_STRING", "ConnectionStrings:DefaultConnection");

        SetFromEnv("JWT_SECRET", "Jwt:Secret");

        SetFromEnv("ADMIN_USERNAME", "Admin:Username");
        SetFromEnv("ADMIN_PASSWORD", "Admin:Password");

        SetFromEnv("PAYMENT_BANK_NAME", "Payment:BankName");
        SetFromEnv("PAYMENT_ACCOUNT_NUMBER", "Payment:AccountNumber");
        SetFromEnv("PAYMENT_ACCOUNT_HOLDER", "Payment:AccountHolder");

        // Prefer nested key; flat OWNER_PHONE still works via environment.
        SetFromEnv("OWNER_PHONE", "Owner:Phone");

        SetFromEnv("HANGFIRE_DASHBOARD_USER", "Hangfire:DashboardUser");
        SetFromEnv("HANGFIRE_DASHBOARD_PASS", "Hangfire:DashboardPassword");
    }
}
