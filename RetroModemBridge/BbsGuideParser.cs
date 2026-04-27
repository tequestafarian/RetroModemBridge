using System.IO.Compression;

namespace RetroModemBridge;

public static class BbsGuideParser
{
    public static string BundledCsvPath => Path.Combine(AppContext.BaseDirectory, "Data", "TelnetBbsGuide", "bbslist.csv");

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
            _ => throw new InvalidOperationException("Select a Telnet BBS Guide .zip file or bbslist.csv file.")
        };
    }

    private static List<BbsGuideEntry> LoadZip(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var csvEntry = archive.Entries.FirstOrDefault(e => string.Equals(Path.GetFileName(e.FullName), "bbslist.csv", StringComparison.OrdinalIgnoreCase));
        if (csvEntry is null)
            throw new InvalidOperationException("This ZIP does not contain bbslist.csv.");

        using var stream = csvEntry.Open();
        using var reader = new StreamReader(stream);
        return ParseCsv(reader.ReadToEnd());
    }

    private static List<BbsGuideEntry> LoadCsv(string csvPath)
    {
        return ParseCsv(File.ReadAllText(csvPath));
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
