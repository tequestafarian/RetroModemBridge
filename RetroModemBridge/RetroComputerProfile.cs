namespace RetroModemBridge;

public sealed class RetroComputerProfile
{
    public string Name { get; set; } = string.Empty;
    public string? ComPort { get; set; }
    public int BaudRate { get; set; } = 19200;
    public bool DtrEnable { get; set; } = true;
    public bool RtsEnable { get; set; } = true;
    public bool EchoEnabled { get; set; }
    public bool TelnetFilteringEnabled { get; set; } = true;
    public string Notes { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? "Unnamed profile" : Name;
}
