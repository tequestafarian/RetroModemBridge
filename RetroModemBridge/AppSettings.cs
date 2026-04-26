using System.Text.Json;

namespace RetroModemBridge;

public sealed class AppSettings
{
    public string? ComPort { get; set; }
    public int BaudRate { get; set; } = 19200;
    public int DefaultTcpPort { get; set; } = 23;
    public bool DtrEnable { get; set; } = true;
    public bool RtsEnable { get; set; } = true;
    public bool EchoEnabled { get; set; } = false;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RetroModemBridge",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
