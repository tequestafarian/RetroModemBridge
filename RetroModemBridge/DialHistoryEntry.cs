namespace RetroModemBridge;

public sealed class DialHistoryEntry
{
    public DateTime DialedAt { get; set; } = DateTime.Now;
    public string DialedText { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 23;
    public string Result { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }

    public string Target => string.IsNullOrWhiteSpace(Host) ? DialedText : $"{Host}:{Port}";
}
