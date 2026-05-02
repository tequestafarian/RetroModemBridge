using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RetroModemBridge;

public enum GuideDownloadKind
{
    Monthly,
    Daily
}

public sealed class GuideDownloadInfo
{
    public GuideDownloadKind Kind { get; init; }
    public string Url { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public sealed class GuideUpdateResult
{
    public GuideDownloadKind Kind { get; init; }
    public string DownloadUrl { get; init; } = string.Empty;
    public string CsvPath { get; init; } = string.Empty;
    public int EntryCount { get; init; }
    public DateTime DownloadedAt { get; init; } = DateTime.Now;
    public string Message { get; init; } = string.Empty;
}

public sealed class GuideUpdateMeta
{
    public string Source { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime DownloadedAt { get; set; }
    public int EntryCount { get; set; }
}

public static class TelnetBbsGuideUpdater
{
    public const string DownloadPageUrl = "https://www.telnetbbsguide.com/lists/download-list/";

    private static readonly string GuideDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RetroModemBridge",
        "TelnetBbsGuide");

    public static string CurrentCsvPath => Path.Combine(GuideDir, "current-bbslist.csv");
    public static string LastZipPath => Path.Combine(GuideDir, "last-download.zip");
    public static string MetaPath => Path.Combine(GuideDir, "guide-meta.json");

    public static bool HasCurrentGuide => File.Exists(CurrentCsvPath);

    public static string GetPreferredCsvPath()
    {
        return File.Exists(CurrentCsvPath) ? CurrentCsvPath : BbsGuideParser.BundledCsvPath;
    }

    public static GuideUpdateMeta? LoadMeta()
    {
        try
        {
            if (!File.Exists(MetaPath))
                return null;

            return JsonSerializer.Deserialize<GuideUpdateMeta>(File.ReadAllText(MetaPath));
        }
        catch
        {
            return null;
        }
    }

    public static string GetStatusText()
    {
        var meta = LoadMeta();
        if (meta is null)
            return "Using bundled guide. No downloaded update installed yet.";

        return $"Using {meta.Source} guide downloaded {meta.DownloadedAt:g}. Entries: {meta.EntryCount}.";
    }

    public static async Task<IReadOnlyList<GuideDownloadInfo>> FindGuideDownloadsAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var html = await client.GetStringAsync(DownloadPageUrl, cancellationToken).ConfigureAwait(false);

        if (LooksLikeVerificationPage(html))
            throw new InvalidOperationException("The download page returned a verification page. Open the page in your browser or load a ZIP/CSV manually.");

        return FindGuideDownloadsFromHtml(html);
    }

    public static IReadOnlyList<GuideDownloadInfo> FindGuideDownloadsFromHtml(string html)
    {
        var results = new List<GuideDownloadInfo>();
        var pageUri = new Uri(DownloadPageUrl);

        foreach (Match match in Regex.Matches(
            html,
            "href\\s*=\\s*[\"'](?<url>[^\"']+\\.zip[^\"']*)[\"'][^>]*>(?<text>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var rawUrl = WebUtility.HtmlDecode(match.Groups["url"].Value);
            var text = CleanHtml(match.Groups["text"].Value);
            var fullUrl = new Uri(pageUri, rawUrl).ToString();

            var windowStart = Math.Max(0, match.Index - 500);
            var windowLength = Math.Min(html.Length - windowStart, match.Length + 1000);
            var surrounding = WebUtility.HtmlDecode(html.Substring(windowStart, windowLength)).ToLowerInvariant();
            var combined = (text + " " + fullUrl + " " + surrounding).ToLowerInvariant();

            GuideDownloadKind? kind = null;

            if (combined.Contains("monthly"))
                kind = GuideDownloadKind.Monthly;
            else if (combined.Contains("daily"))
                kind = GuideDownloadKind.Daily;

            if (kind is null)
                continue;

            if (results.Any(r => r.Kind == kind.Value))
                continue;

            results.Add(new GuideDownloadInfo
            {
                Kind = kind.Value,
                Url = fullUrl,
                Label = string.IsNullOrWhiteSpace(text) ? Path.GetFileName(new Uri(fullUrl).LocalPath) : text.Trim()
            });
        }

        return results;
    }

    public static async Task<GuideUpdateResult> DownloadAndInstallAsync(
        GuideDownloadKind kind,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (kind == GuideDownloadKind.Daily)
            return await DownloadAndInstallDailyOnlyAsync(progress, cancellationToken).ConfigureAwait(false);

        // Monthly path intentionally left as the original working path.
        progress?.Report("10|Reading Telnet BBS Guide download page");
        var links = await FindGuideDownloadsAsync(cancellationToken).ConfigureAwait(false);
        var link = links.FirstOrDefault(l => l.Kind == GuideDownloadKind.Monthly);

        if (link is null)
            throw new InvalidOperationException("Could not find the Monthly ZIP link on the Telnet BBS Guide download page.");

        progress?.Report("25|Found latest Monthly ZIP");
        return await DownloadAndInstallFromUrlAsync(GuideDownloadKind.Monthly, link.Url, progress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<GuideUpdateResult> DownloadAndInstallDailyOnlyAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("10|Reading Telnet BBS Guide download page");
        var dailyLink = await FindDailyDownloadOnlyAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report("25|Found Download Daily link");
        Directory.CreateDirectory(GuideDir);

        progress?.Report("35|Downloading Daily guide ZIP");
        using var client = CreateClient();
        var bytes = await client.GetByteArrayAsync(dailyLink.Url, cancellationToken).ConfigureAwait(false);

        if (bytes.Length < 100)
            throw new InvalidOperationException("Downloaded Daily file was too small to be a valid guide ZIP.");

        await File.WriteAllBytesAsync(LastZipPath, bytes, cancellationToken).ConfigureAwait(false);

        progress?.Report("65|Parsing Daily dialdirectory.xml");
        return InstallDailyZipOnly(LastZipPath, dailyLink.Url, progress);
    }

    private static async Task<GuideDownloadInfo> FindDailyDownloadOnlyAsync(CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        var html = await client.GetStringAsync(DownloadPageUrl, cancellationToken).ConfigureAwait(false);

        if (LooksLikeVerificationPage(html))
            throw new InvalidOperationException("The download page returned a verification page. Open the page in your browser or load a ZIP/CSV manually.");

        var pageUri = new Uri(DownloadPageUrl);

        foreach (Match match in Regex.Matches(
            html,
            "<a\\b(?<attrs>[^>]*)>(?<text>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var text = CleanHtml(match.Groups["text"].Value);
            if (!text.Contains("Download Daily", StringComparison.OrdinalIgnoreCase))
                continue;

            var attrs = match.Groups["attrs"].Value;
            var hrefMatch = Regex.Match(
                attrs,
                "href\\s*=\\s*[\"'](?<url>[^\"']+)[\"']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!hrefMatch.Success)
                continue;

            var rawUrl = WebUtility.HtmlDecode(hrefMatch.Groups["url"].Value).Trim();
            var fullUrl = new Uri(pageUri, rawUrl).ToString();

            return new GuideDownloadInfo
            {
                Kind = GuideDownloadKind.Daily,
                Url = fullUrl,
                Label = text
            };
        }

        throw new InvalidOperationException("Could not find the Download Daily link on the Telnet BBS Guide download page.");
    }

    private static GuideUpdateResult InstallDailyZipOnly(string zipPath, string sourceUrl, IProgress<string>? progress)
    {
        Directory.CreateDirectory(GuideDir);

        using var archive = ZipFile.OpenRead(zipPath);
        var xmlEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(e.FullName), "dialdirectory.xml", StringComparison.OrdinalIgnoreCase));

        if (xmlEntry is null)
            throw new InvalidOperationException("The Daily guide ZIP did not contain dialdirectory.xml.");

        // Reuse the parser's Daily fallback. It will read dialdirectory.xml,
        // clean bad ampersands, and normalize entries.
        var entriesFromDaily = BbsGuideParser.LoadZip(zipPath);
        if (entriesFromDaily.Count == 0)
            throw new InvalidOperationException("The Daily guide ZIP was parsed, but no Telnet entries were found.");

        BbsGuideParser.SaveNormalizedCsv(entriesFromDaily, CurrentCsvPath);

        progress?.Report("85|Saving Daily guide");
        var entries = BbsGuideParser.LoadFile(CurrentCsvPath);

        var meta = new GuideUpdateMeta
        {
            Source = GuideDownloadKind.Daily.ToString(),
            DownloadUrl = sourceUrl,
            DownloadedAt = DateTime.Now,
            EntryCount = entries.Count
        };

        File.WriteAllText(MetaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

        progress?.Report("100|Guide update complete");

        return new GuideUpdateResult
        {
            Kind = GuideDownloadKind.Daily,
            DownloadUrl = sourceUrl,
            CsvPath = CurrentCsvPath,
            EntryCount = entries.Count,
            DownloadedAt = meta.DownloadedAt,
            Message = $"Installed Daily guide with {entries.Count} entries."
        };
    }

    public static async Task<GuideUpdateResult> DownloadAndInstallFromUrlAsync(
        GuideDownloadKind kind,
        string url,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(GuideDir);

        progress?.Report("35|Downloading guide ZIP");
        using var client = CreateClient();
        var bytes = await client.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);

        if (bytes.Length < 100)
            throw new InvalidOperationException("Downloaded file was too small to be a valid guide ZIP.");

        await File.WriteAllBytesAsync(LastZipPath, bytes, cancellationToken).ConfigureAwait(false);

        progress?.Report("65|Extracting bbslist.csv");
        return InstallZip(LastZipPath, kind, url, progress);
    }

    public static GuideUpdateResult InstallZip(string zipPath, GuideDownloadKind kind, string sourceUrl, IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(GuideDir);

        using var archive = ZipFile.OpenRead(zipPath);
        var csvEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(e.FullName), "bbslist.csv", StringComparison.OrdinalIgnoreCase));

        if (csvEntry is not null)
        {
            // Original monthly path restored: extract bbslist.csv exactly as before.
            csvEntry.ExtractToFile(CurrentCsvPath, overwrite: true);
        }
        else
        {
            // Daily fallback: parse dialdirectory.xml and normalize it into RMB's current-bbslist.csv.
            var xmlEntry = archive.Entries.FirstOrDefault(e =>
                string.Equals(Path.GetFileName(e.FullName), "dialdirectory.xml", StringComparison.OrdinalIgnoreCase));

            if (xmlEntry is null)
                throw new InvalidOperationException("This ZIP does not contain bbslist.csv or dialdirectory.xml.");

            var entriesFromDaily = BbsGuideParser.LoadZip(zipPath);
            if (entriesFromDaily.Count == 0)
                throw new InvalidOperationException("The daily guide ZIP was parsed, but no Telnet entries were found.");

            BbsGuideParser.SaveNormalizedCsv(entriesFromDaily, CurrentCsvPath);
        }

        progress?.Report("85|Parsing updated guide");
        var entries = BbsGuideParser.LoadFile(CurrentCsvPath);

        var meta = new GuideUpdateMeta
        {
            Source = kind.ToString(),
            DownloadUrl = sourceUrl,
            DownloadedAt = DateTime.Now,
            EntryCount = entries.Count
        };

        File.WriteAllText(MetaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

        progress?.Report("100|Guide update complete");

        return new GuideUpdateResult
        {
            Kind = kind,
            DownloadUrl = sourceUrl,
            CsvPath = CurrentCsvPath,
            EntryCount = entries.Count,
            DownloadedAt = meta.DownloadedAt,
            Message = $"Installed {kind} guide with {entries.Count} entries."
        };
    }

    public static GuideUpdateResult InstallCsv(string csvPath, GuideDownloadKind kind, string sourceLabel = "Manual file", IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(GuideDir);
        progress?.Report("70|Installing CSV");

        File.Copy(csvPath, CurrentCsvPath, overwrite: true);
        var entries = BbsGuideParser.LoadFile(CurrentCsvPath);

        var meta = new GuideUpdateMeta
        {
            Source = kind.ToString(),
            DownloadUrl = sourceLabel,
            DownloadedAt = DateTime.Now,
            EntryCount = entries.Count
        };

        File.WriteAllText(MetaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
        progress?.Report("100|Guide update complete");

        return new GuideUpdateResult
        {
            Kind = kind,
            DownloadUrl = sourceLabel,
            CsvPath = CurrentCsvPath,
            EntryCount = entries.Count,
            DownloadedAt = meta.DownloadedAt,
            Message = $"Installed {kind} guide with {entries.Count} entries."
        };
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(45);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RetroModemBridge/3.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        return client;
    }

    private static bool LooksLikeVerificationPage(string html)
    {
        var lower = html.ToLowerInvariant();
        return lower.Contains("verify you are human")
            || lower.Contains("checking your browser")
            || lower.Contains("cloudflare")
            || lower.Contains("request is being verified");
    }

    private static string CleanHtml(string html)
    {
        var noTags = Regex.Replace(html, "<.*?>", " ");
        noTags = WebUtility.HtmlDecode(noTags);
        return Regex.Replace(noTags, "\\s+", " ").Trim();
    }
}
