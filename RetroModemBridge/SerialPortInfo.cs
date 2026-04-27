using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace RetroModemBridge;

public sealed record SerialPortInfo(string PortName, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public static class SerialPortDiscovery
{
    private static readonly Regex ComRegex = new(@"\((COM\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<SerialPortInfo> GetPorts()
    {
        var ports = SerialPort.GetPortNames()
            .OrderBy(NaturalComSortKey)
            .ToList();

        var friendlyNames = GetFriendlyNames();
        return ports
            .Select(port => new SerialPortInfo(
                port,
                friendlyNames.TryGetValue(port, out var friendly)
                    ? $"{port} - {friendly}"
                    : port))
            .ToList();
    }

    private static Dictionary<string, string> GetFriendlyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

            foreach (var device in searcher.Get().Cast<ManagementObject>())
            {
                var name = device["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var match = ComRegex.Match(name);
                if (!match.Success)
                    continue;

                var port = match.Groups[1].Value.ToUpperInvariant();
                var friendly = ComRegex.Replace(name, string.Empty).Trim();
                names[port] = friendly;
            }
        }
        catch
        {
        }

        return names;
    }

    public static int NaturalComSortKey(string portName)
    {
        var digits = new string(portName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : int.MaxValue;
    }
}
