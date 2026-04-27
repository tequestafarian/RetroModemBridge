using System.Text.Json;

namespace RetroModemBridge;

public sealed class AppSettings
{
    public string? ComPort { get; set; }
    public bool RememberComPort { get; set; } = true;
    public int BaudRate { get; set; } = 19200;
    public int DefaultTcpPort { get; set; } = 23;
    public bool DtrEnable { get; set; } = true;
    public bool RtsEnable { get; set; } = true;
    public bool EchoEnabled { get; set; } = false;
    public bool TelnetFilteringEnabled { get; set; } = true;
    public List<BbsEntry> DialDirectory { get; set; } = CreateDefaultDirectory();

    private static string PortableDir => AppContext.BaseDirectory;
    private static string PortableSettingsPath => Path.Combine(PortableDir, "settings-v2-beta.json");
    private static string AppDataSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RetroModemBridge",
        "settings-v2-beta.json");

    public static string SettingsPath => CanWritePortableSettings() ? PortableSettingsPath : AppDataSettingsPath;

    public static AppSettings Load()
    {
        try
        {
            var path = File.Exists(PortableSettingsPath) ? PortableSettingsPath : AppDataSettingsPath;
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            if (settings.DialDirectory.Count == 0)
                settings.DialDirectory = CreateDefaultDirectory();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        var path = SettingsPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static bool CanWritePortableSettings()
    {
        try
        {
            Directory.CreateDirectory(PortableDir);
            var testPath = Path.Combine(PortableDir, ".write-test.tmp");
            File.WriteAllText(testPath, "test");
            File.Delete(testPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<BbsEntry> CreateDefaultDirectory() =>
    [
        new BbsEntry { Alias = "1", Name = "Dark Realms", Host = "darkrealms.ca", Port = 23, Notes = "ANSI BBS" },
        new BbsEntry { Alias = "coco", Name = "CoCoNet", Host = "coconet.ddns.net", Port = 6809, Notes = "CoCoNet BBS" }
    ];
}
