namespace RetroModemBridge;

public sealed class BbsEntry
{
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 23;
    public string Notes { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(Name)
        ? $"{Alias} {Host}:{Port}"
        : $"{Alias} {Name} {Host}:{Port}";
}
