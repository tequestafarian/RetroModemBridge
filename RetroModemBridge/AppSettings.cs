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
    public bool PlayStartupSound { get; set; } = false;
    public List<BbsEntry> DialDirectory { get; set; } = CreateDefaultDirectory();
    public List<DialHistoryEntry> DialHistory { get; set; } = new();
    public List<RetroComputerProfile> Profiles { get; set; } = CreateDefaultProfiles();
    public string FeaturedBbsAlias { get; set; } = "1";

    private static string PortableDir => AppContext.BaseDirectory;
    private static string PortableSettingsPath => Path.Combine(PortableDir, "settings-v3.4.json");
    private static string AppDataSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RetroModemBridge",
        "settings-v3.4.json");

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
            if (settings.Profiles.Count == 0)
                settings.Profiles = CreateDefaultProfiles();
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

    private static List<RetroComputerProfile> CreateDefaultProfiles() =>
    [
        new RetroComputerProfile { Name = "CoCo 3 / NetMate / 19200", BaudRate = 19200, DtrEnable = true, RtsEnable = true, EchoEnabled = false, TelnetFilteringEnabled = true, Notes = "Good starting profile for CoCo 3 with Deluxe RS-232 Pak and NetMate." },
        new RetroComputerProfile { Name = "Generic 9600 8-N-1", BaudRate = 9600, DtrEnable = true, RtsEnable = true, EchoEnabled = false, TelnetFilteringEnabled = true, Notes = "Safe generic serial profile." }
    ];

    private static List<BbsEntry> CreateDefaultDirectory() =>
    [
        new BbsEntry { Alias = "1", Name = "Dark Realms", Host = "darkrealms.ca", Port = 23, EntryType = "Telnet", Category = "General BBS", SystemType = "Telnet BBS", SupportsAnsi = true, IsFavorite = true, Notes = "ANSI BBS" },
        new BbsEntry { Alias = "coco", Name = "CoCoNet", Host = "coconet.ddns.net", Port = 6809, EntryType = "Telnet", Category = "CoCo / Tandy", SystemType = "CoCo BBS", SupportsAnsi = true, IsFavorite = true, Notes = "CoCoNet BBS" },
        new BbsEntry { Alias = "usurper", Name = "Usurper Reborn", Host = "local-door", Port = 0, EntryType = "Door", Category = "Door Games", SystemType = "Local Door", SupportsAnsi = true, IsFavorite = true, DoorDropFileType = "DOOR32.SYS", DoorNodeNumber = 1, DoorUserName = "CoCo Caller", Notes = "Example local door entry. Edit it and point Executable to Usurper Reborn." }
    ];
}
