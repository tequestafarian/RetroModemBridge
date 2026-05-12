namespace RetroModemBridge;

public sealed class IrcPreset
{
    public string Alias { get; set; } = "irc";
    public string Name { get; set; } = "Retro IRC";
    public string Server { get; set; } = "irc.libera.chat";
    public int Port { get; set; } = 6697;
    public bool UseTls { get; set; } = true;
    public string Channel { get; set; } = "#retromodem";
    public string Nickname { get; set; } = "RMBUser";
    public string RealName { get; set; } = "RetroModem Bridge user";
    public bool StripFormatting { get; set; } = true;
    public bool ShowJoinPartNoise { get; set; } = false;
    public string Notes { get; set; } = "Dial with ATDT IRC or ATDT IRC-LIBERA.";

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Alias : Name;
}
