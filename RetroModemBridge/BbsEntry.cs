namespace RetroModemBridge;

public sealed class BbsEntry
{
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 23;
    public string Category { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public bool SupportsAnsi { get; set; } = true;
    public bool IsFavorite { get; set; }
    public DateTime? LastDialed { get; set; }
    public string LastResult { get; set; } = string.Empty;
    public DateTime? LastChecked { get; set; }
    public int? LastResponseMs { get; set; }
    public string Notes { get; set; } = string.Empty;

    public string DialTarget => $"{Host}:{Port}";

    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? (string.IsNullOrWhiteSpace(Alias) ? DialTarget : Alias)
        : Name;

    public override string ToString() => string.IsNullOrWhiteSpace(Name)
        ? $"{Alias} {Host}:{Port}"
        : $"{Alias} {Name} {Host}:{Port}";
}
