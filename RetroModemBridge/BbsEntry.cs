namespace RetroModemBridge;

public sealed class BbsEntry
{
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 23;
    public string EntryType { get; set; } = "Telnet";
    public string Category { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public bool SupportsAnsi { get; set; } = true;
    public bool IsFavorite { get; set; }
    public DateTime? LastDialed { get; set; }
    public string LastResult { get; set; } = string.Empty;
    public DateTime? LastChecked { get; set; }
    public int? LastResponseMs { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string DoorExecutablePath { get; set; } = string.Empty;
    public string DoorWorkingDirectory { get; set; } = string.Empty;
    public string DoorArguments { get; set; } = string.Empty;
    public string DoorDropFileType { get; set; } = "DOOR32.SYS";
    public int DoorNodeNumber { get; set; } = 1;
    public string DoorUserName { get; set; } = "CoCo Caller";
    public bool DoorAutoEnterSingleKeys { get; set; } = true;
    public bool DoorPauseLongOutput { get; set; } = true;
    public int DoorLinesPerPage { get; set; } = 21;
    public string DoorMorePrompt { get; set; } = "-- More -- Space/Enter=next, B=back -- ";
    public int DoorMorePromptRow { get; set; } = 24;

    public bool IsDoorGame => string.Equals(EntryType, "Door", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(DoorExecutablePath);

    public string DialTarget => IsDoorGame
        ? (string.IsNullOrWhiteSpace(DoorExecutablePath) ? "Local door game" : DoorExecutablePath)
        : $"{Host}:{Port}";

    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? (string.IsNullOrWhiteSpace(Alias) ? DialTarget : Alias)
        : Name;

    public override string ToString() => IsDoorGame
        ? $"{Alias} {DisplayName} [local door]"
        : (string.IsNullOrWhiteSpace(Name)
            ? $"{Alias} {Host}:{Port}"
            : $"{Alias} {Name} {Host}:{Port}");
}
