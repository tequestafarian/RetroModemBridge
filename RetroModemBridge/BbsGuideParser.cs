using System.IO.Compression;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace RetroModemBridge;

public static class BbsGuideParser
{
    public static string BundledCsvPath => Path.Combine(AppContext.BaseDirectory, "Data", "TelnetBbsGuide", "bbslist.csv");

    public static List<BbsGuideEntry> LoadCurrentList()
    {
        var path = TelnetBbsGuideUpdater.GetPreferredCsvPath();
        if (!File.Exists(path))
            throw new FileNotFoundException("No Telnet BBS Guide CSV was found.", path);

        return LoadCsv(path);
    }

    public static List<BbsGuideEntry> LoadBundledList()
    {
        if (!File.Exists(BundledCsvPath))
            throw new FileNotFoundException("The bundled Telnet BBS Guide CSV was not found.", BundledCsvPath);

        return LoadCsv(BundledCsvPath);
    }

    public static List<BbsGuideEntry> LoadFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".csv" => LoadCsv(path),
            ".zip" => LoadZip(path),
            ".xml" => LoadEtherTermXml(File.ReadAllText(path)),
            _ => throw new InvalidOperationException("Select a Telnet BBS Guide .zip, .csv, or .xml file.")
        };
    }

    public static List<BbsGuideEntry> LoadZip(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        // Keep the original monthly behavior first.
        // Monthly ZIPs contain bbslist.csv and should use that exact file.
        var csvEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(e.FullName), "bbslist.csv", StringComparison.OrdinalIgnoreCase));

        if (csvEntry is not null)
        {
            using var stream = csvEntry.Open();
            using var reader = new StreamReader(stream);
            return ParseCsv(reader.ReadToEnd());
        }

        // Daily ZIP fallback. The daily archive may contain dialdirectory.xml instead.
        var xmlEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(e.FullName), "dialdirectory.xml", StringComparison.OrdinalIgnoreCase));

        if (xmlEntry is not null)
        {
            using var stream = xmlEntry.Open();
            using var reader = new StreamReader(stream);
            return LoadEtherTermXml(reader.ReadToEnd());
        }

        throw new InvalidOperationException("This ZIP does not contain bbslist.csv or dialdirectory.xml.");
    }

    private static List<BbsGuideEntry> LoadCsv(string csvPath)
    {
        return ParseCsv(File.ReadAllText(csvPath));
    }

    public static void SaveNormalizedCsv(IEnumerable<BbsGuideEntry> entries, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(path, false);
        writer.WriteLine("bbsName,TelnetAddress,bbsPort,software,location");

        foreach (var entry in entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(e.Host))
            .GroupBy(e => (Host: e.Host.Trim().ToLowerInvariant(), e.Port))
            .Select(g => g.First())
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteLine(string.Join(",",
                CsvEscape(entry.Name),
                CsvEscape(entry.Host),
                entry.Port.ToString(),
                CsvEscape(entry.Software),
                CsvEscape(entry.Location)));
        }
    }

    private static string CsvEscape(string value)
    {
        value = value.Trim();
        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }

    private static string SanitizeLooseXml(string xml)
    {
        return Regex.Replace(
            xml,
            "&(?!amp;|lt;|gt;|quot;|apos;|#[0-9]+;|#x[0-9A-Fa-f]+;)",
            "&amp;",
            RegexOptions.CultureInvariant);
    }

    private static List<BbsGuideEntry> LoadEtherTermXml(string xml)
    {
        var entries = new List<BbsGuideEntry>();
        var doc = XDocument.Parse(SanitizeLooseXml(xml));

        foreach (var bbs in doc.Descendants("BBS"))
        {
            var name = (string?)bbs.Attribute("name") ?? string.Empty;
            var host = (string?)bbs.Attribute("ip") ?? string.Empty;
            var portText = (string?)bbs.Attribute("port") ?? "23";
            var protocol = ((string?)bbs.Attribute("protocol") ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(protocol) &&
                !protocol.Equals("TELNET", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(host))
                continue;

            var port = 23;
            if (int.TryParse(portText, out var parsedPort) && parsedPort is >= 1 and <= 65535)
                port = parsedPort;

            entries.Add(new BbsGuideEntry
            {
                Name = name.Trim(),
                Host = host.Trim(),
                Port = port,
                Software = "Telnet BBS Guide Daily",
                Location = string.Empty,
                Source = "Telnet BBS Guide Daily"
            });
        }

        return entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(e.Host))
            .GroupBy(e => (Host: e.Host.Trim().ToLowerInvariant(), e.Port))
            .Select(g => g.First())
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<BbsGuideEntry> ParseCsv(string csv)
    {
        var rows = ReadCsvRows(csv).ToList();
        if (rows.Count == 0)
            return [];

        var header = rows[0];
        var indexes = header
            .Select((name, index) => new { name, index })
            .ToDictionary(x => x.name.Trim(), x => x.index, StringComparer.OrdinalIgnoreCase);

        string Get(IReadOnlyList<string> row, string column)
        {
            if (!indexes.TryGetValue(column, out var index))
                return string.Empty;
            return index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;
        }

        var entries = new List<BbsGuideEntry>();
        foreach (var row in rows.Skip(1))
        {
            var name = Get(row, "bbsName");
            var host = Get(row, "TelnetAddress");
            var portText = Get(row, "bbsPort");
            var software = Get(row, "software");
            var location = Get(row, "location");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(host))
                continue;

            var port = 23;
            if (int.TryParse(portText, out var parsedPort) && parsedPort is >= 1 and <= 65535)
                port = parsedPort;

            // Skip SSH-only entries by default because RetroModem Bridge is a Telnet/TCP bridge.
            if (string.IsNullOrWhiteSpace(portText) && !string.IsNullOrWhiteSpace(Get(row, "sshPort")))
                continue;

            entries.Add(new BbsGuideEntry
            {
                Name = name,
                Host = host,
                Port = port,
                Software = software,
                Location = location,
                Source = "Telnet BBS Guide"
            });
        }

        return entries
            .GroupBy(e => (Host: e.Host.ToLowerInvariant(), e.Port))
            .Select(g => g.First())
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<List<string>> ReadCsvRows(string text)
    {
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                continue;
            }

            if (ch == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (ch == '\r')
                continue;

            if (ch == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
                    yield return row;
                row = [];
                continue;
            }

            field.Append(ch);
        }

        row.Add(field.ToString());
        if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
            yield return row;
    }
}
