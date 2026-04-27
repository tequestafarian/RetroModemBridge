namespace RetroModemBridge;

public sealed class BbsGuideEntry
{
    public bool Selected { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 23;
    public string Software { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Source { get; set; } = "Telnet BBS Guide";

    public string HostPort => $"{Host}:{Port}";

    public BbsEntry ToDirectoryEntry(string alias)
    {
        var notesParts = new List<string> { "Imported from Telnet BBS Guide" };
        if (!string.IsNullOrWhiteSpace(Software))
            notesParts.Add("Software: " + Software.Trim());
        if (!string.IsNullOrWhiteSpace(Location))
            notesParts.Add("Location: " + Location.Trim());

        return new BbsEntry
        {
            Alias = alias,
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port < 1 || Port > 65535 ? 23 : Port,
            Notes = string.Join("; ", notesParts)
        };
    }
}
