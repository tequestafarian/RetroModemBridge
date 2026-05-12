param(
    [string]$ProjectRoot = (Get-Location).Path,
    [switch]$Build
)

$ErrorActionPreference = 'Stop'

function Fail([string]$msg) {
    Write-Host "ERROR: $msg" -ForegroundColor Red
    exit 1
}

function N([string]$s) {
    return ($s -replace "`r`n", "`n")
}

function WriteUtf8([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function ReplaceLiteral([string]$Path, [string]$Old, [string]$New, [string]$Label) {
    $text = N([System.IO.File]::ReadAllText($Path))
    $oldN = N($Old)
    $newN = N($New)
    if ($text.Contains($newN.Trim())) {
        Write-Host "Already patched: $Label" -ForegroundColor DarkGray
        return
    }
    if (-not $text.Contains($oldN)) {
        Fail "Could not find patch point for $Label in $Path"
    }
    $text = $text.Replace($oldN, $newN)
    WriteUtf8 $Path $text
    Write-Host "Patched: $Label" -ForegroundColor Green
}

function InsertBeforeLiteral([string]$Path, [string]$Needle, [string]$Insert, [string]$Label) {
    $text = N([System.IO.File]::ReadAllText($Path))
    $needleN = N($Needle)
    $insertN = N($Insert)
    if ($text.Contains($insertN.Trim())) {
        Write-Host "Already patched: $Label" -ForegroundColor DarkGray
        return
    }
    if (-not $text.Contains($needleN)) { Fail "Could not find patch point for $Label in $Path" }
    $text = $text.Replace($needleN, $insertN + $needleN)
    WriteUtf8 $Path $text
    Write-Host "Patched: $Label" -ForegroundColor Green
}

function InsertAfterLiteral([string]$Path, [string]$Needle, [string]$Insert, [string]$Label) {
    $text = N([System.IO.File]::ReadAllText($Path))
    $needleN = N($Needle)
    $insertN = N($Insert)
    if ($text.Contains($insertN.Trim())) {
        Write-Host "Already patched: $Label" -ForegroundColor DarkGray
        return
    }
    if (-not $text.Contains($needleN)) { Fail "Could not find patch point for $Label in $Path" }
    $text = $text.Replace($needleN, $needleN + $insertN)
    WriteUtf8 $Path $text
    Write-Host "Patched: $Label" -ForegroundColor Green
}

$projectDir = Join-Path $ProjectRoot 'RetroModemBridge'
if (-not (Test-Path (Join-Path $projectDir 'RetroModemBridge.csproj'))) {
    Fail "Could not find RetroModemBridge\RetroModemBridge.csproj. Run this from the repo root."
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageRoot = Split-Path -Parent $scriptDir

function Copy-PackageFileSafe([string]$Source, [string]$Destination) {
    if (-not (Test-Path $Source)) {
        Fail "Missing package file: $Source"
    }

    $sourceFull = [System.IO.Path]::GetFullPath($Source)
    $destFull = [System.IO.Path]::GetFullPath($Destination)

    if ([string]::Equals($sourceFull, $destFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Host "Already in place: $(Split-Path -Leaf $Destination)" -ForegroundColor DarkGray
        return
    }

    Copy-Item $Source $Destination -Force
    Write-Host "Copied: $(Split-Path -Leaf $Destination)" -ForegroundColor Green
}

Copy-PackageFileSafe (Join-Path $packageRoot 'RetroModemBridge\IrcPreset.cs') (Join-Path $projectDir 'IrcPreset.cs')
Copy-PackageFileSafe (Join-Path $packageRoot 'RetroModemBridge\IrcBridgeSession.cs') (Join-Path $projectDir 'IrcBridgeSession.cs')
Write-Host "IRC source files ready." -ForegroundColor Green

$appSettings = Join-Path $projectDir 'AppSettings.cs'
$modemBridge = Join-Path $projectDir 'ModemBridge.cs'
$mainForm = Join-Path $projectDir 'MainForm.cs'
$csproj = Join-Path $projectDir 'RetroModemBridge.csproj'

Copy-Item $appSettings "$appSettings.irc-backup" -Force
Copy-Item $modemBridge "$modemBridge.irc-backup" -Force
Copy-Item $mainForm "$mainForm.irc-backup" -Force
Copy-Item $csproj "$csproj.irc-backup" -Force

ReplaceLiteral $appSettings @'
    public List<RetroComputerProfile> Profiles { get; set; } = CreateDefaultProfiles();
    public string FeaturedBbsAlias { get; set; } = "1";
'@ @'
    public List<RetroComputerProfile> Profiles { get; set; } = CreateDefaultProfiles();
    public List<IrcPreset> IrcPresets { get; set; } = CreateDefaultIrcPresets();
    public string FeaturedBbsAlias { get; set; } = "1";
'@ 'AppSettings IrcPresets property'

ReplaceLiteral $appSettings @'
            if (settings.Profiles.Count == 0)
                settings.Profiles = CreateDefaultProfiles();
            return settings;
'@ @'
            if (settings.Profiles.Count == 0)
                settings.Profiles = CreateDefaultProfiles();
            if (settings.IrcPresets.Count == 0)
                settings.IrcPresets = CreateDefaultIrcPresets();
            return settings;
'@ 'AppSettings default IRC presets on load'

InsertAfterLiteral $appSettings @'
    private static List<RetroComputerProfile> CreateDefaultProfiles() =>
    [
        new RetroComputerProfile { Name = "CoCo 3 / NetMate / 19200", BaudRate = 19200, DtrEnable = true, RtsEnable = true, EchoEnabled = false, TelnetFilteringEnabled = true, Notes = "Good starting profile for CoCo 3 with Deluxe RS-232 Pak and NetMate." },
        new RetroComputerProfile { Name = "Generic 9600 8-N-1", BaudRate = 9600, DtrEnable = true, RtsEnable = true, EchoEnabled = false, TelnetFilteringEnabled = true, Notes = "Safe generic serial profile." }
    ];
'@ @'

    private static List<IrcPreset> CreateDefaultIrcPresets() =>
    [
        new IrcPreset { Alias = "irc", Name = "RetroModem IRC", Server = "irc.libera.chat", Port = 6697, UseTls = true, Channel = "#retromodem", Nickname = "RMBUser", RealName = "RetroModem Bridge user", StripFormatting = true, ShowJoinPartNoise = false, Notes = "Default IRC bridge preset. Dial ATDT IRC." },
        new IrcPreset { Alias = "irc-libera", Name = "Libera Chat", Server = "irc.libera.chat", Port = 6697, UseTls = true, Channel = "#retromodem", Nickname = "RMBUser", RealName = "RetroModem Bridge user", StripFormatting = true, ShowJoinPartNoise = false, Notes = "TLS IRC preset for Libera Chat." }
    ];
'@ 'AppSettings default IRC preset method'

$proj = N([System.IO.File]::ReadAllText($csproj))
$proj = $proj.Replace('<AssemblyName>RetroModemBridge-v3.4</AssemblyName>', '<AssemblyName>RetroModemBridge-v3.5-irc</AssemblyName>')
$proj = $proj.Replace('<Version>3.4.0</Version>', '<Version>3.5.0</Version>')
$proj = $proj.Replace('<FileVersion>3.4.0.0</FileVersion>', '<FileVersion>3.5.0.0</FileVersion>')
$proj = $proj.Replace('<AssemblyVersion>3.4.0.0</AssemblyVersion>', '<AssemblyVersion>3.5.0.0</AssemblyVersion>')
WriteUtf8 $csproj $proj
Write-Host "Patched: project version v3.5 IRC" -ForegroundColor Green

ReplaceLiteral $modemBridge @'
    private Process? _doorProcess;
    private CancellationTokenSource? _doorCts;
'@ @'
    private Process? _doorProcess;
    private CancellationTokenSource? _doorCts;
    private IrcBridgeSession? _ircSession;
    private CancellationTokenSource? _ircCts;
'@ 'ModemBridge IRC fields'

ReplaceLiteral $modemBridge @'
    public bool IsDoorConnected => _doorProcess is { HasExited: false };
    public bool IsConnected => IsTcpConnected || IsDoorConnected;
'@ @'
    public bool IsDoorConnected => _doorProcess is { HasExited: false };
    public bool IsIrcConnected => _ircSession?.IsConnected == true;
    public bool IsConnected => IsTcpConnected || IsDoorConnected || IsIrcConnected;
'@ 'ModemBridge IsIrcConnected'

ReplaceLiteral $modemBridge @'
    public IList<BbsEntry> DialDirectory { get; set; } = Array.Empty<BbsEntry>();
    public IList<DialHistoryEntry> DialHistory { get; set; } = new List<DialHistoryEntry>();
'@ @'
    public IList<BbsEntry> DialDirectory { get; set; } = Array.Empty<BbsEntry>();
    public IList<IrcPreset> IrcPresets { get; set; } = Array.Empty<IrcPreset>();
    public IList<DialHistoryEntry> DialHistory { get; set; } = new List<DialHistoryEntry>();
'@ 'ModemBridge IrcPresets property'

ReplaceLiteral $modemBridge @'
            if (IsDoorConnected)
            {
                WriteDoorInput(buffer, read);
                return;
            }

            if (IsTcpConnected)
'@ @'
            if (IsDoorConnected)
            {
                WriteDoorInput(buffer, read);
                return;
            }

            if (IsIrcConnected)
            {
                _ircSession?.HandleSerialBytes(buffer, read);
                return;
            }

            if (IsTcpConnected)
'@ 'ModemBridge route serial input to IRC session'

ReplaceLiteral $modemBridge @'
            if (IsDoorConnected)
            {
                WriteDoorInput(bytes, bytes.Length);
                return;
            }

            if (IsTcpConnected)
'@ @'
            if (IsDoorConnected)
            {
                WriteDoorInput(bytes, bytes.Length);
                return;
            }

            if (IsIrcConnected)
            {
                _ircSession?.HandleSerialBytes(bytes, bytes.Length);
                return;
            }

            if (IsTcpConnected)
'@ 'ModemBridge route mirror input to IRC session'

ReplaceLiteral $modemBridge @'
            SendResponse("BBS Companion features: HELP, TIME, MENU, BBSLIST, DOORS, FAVORITES");
'@ @'
            SendResponse("BBS Companion features: HELP, TIME, MENU, BBSLIST, DOORS, FAVORITES, IRC");
'@ 'ModemBridge ATI feature line'

ReplaceLiteral $modemBridge @'
            SendResponse("Saved door games: " + DialDirectory.Count(e => e.IsDoorGame));
            SendResponse("Dial history entries: " + DialHistory.Count);
'@ @'
            SendResponse("Saved door games: " + DialDirectory.Count(e => e.IsDoorGame));
            SendResponse("IRC presets: " + IrcPresets.Count);
            SendResponse("Dial history entries: " + DialHistory.Count);
'@ 'ModemBridge AT&V IRC preset count'

ReplaceLiteral $modemBridge @'
        if (TryHandleTextService(dialString, started))
            return;

        var resolved = ResolveDialString(dialString);
'@ @'
        if (TryHandleTextService(dialString, started))
            return;

        if (await TryDialIrcAsync(dialString, started).ConfigureAwait(false))
            return;

        var resolved = ResolveDialString(dialString);
'@ 'ModemBridge dial IRC before BBS aliases'

$ircMethods = @'
    private async Task<bool> TryDialIrcAsync(string dialString, DateTime started)
    {
        var key = (dialString ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var preset = IrcPresets.FirstOrDefault(p =>
            string.Equals(p.Alias, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));

        if (preset is null && key.StartsWith("IRC", StringComparison.OrdinalIgnoreCase))
        {
            preset = IrcPresets.FirstOrDefault(p => string.Equals(p.Alias, "irc", StringComparison.OrdinalIgnoreCase))
                     ?? IrcPresets.FirstOrDefault();
        }

        if (preset is null)
            return false;

        LogMessage($"Dial alias {key} resolved to IRC {preset.Server}:{preset.Port} {preset.Channel}.");
        SetStatus($"Dialing IRC {preset.Server}:{preset.Port}...");
        SendResponse("CONNECT");
        WriteSerialText("\r\n*** RetroModem IRC Bridge starting...\r\n");
        WriteSerialText($"*** Server: {preset.Server}:{preset.Port} {(preset.UseTls ? "TLS" : "plain")}\r\n");
        WriteSerialText($"*** Channel: {preset.Channel}\r\n");

        try
        {
            _ircCts = new CancellationTokenSource();
            var encoding = _serialPort?.Encoding ?? Encoding.GetEncoding(437);
            var session = new IrcBridgeSession(
                preset,
                encoding,
                WriteSerialText,
                LogMessage,
                () => TrafficChanged?.Invoke());

            _ircSession = session;
            await session.ConnectAsync(_ircCts.Token).ConfigureAwait(false);
            CurrentConnection = session.CurrentConnection;
            RecordHistory(key, preset.Server, preset.Port, "IRC connected", started);
            TrafficChanged?.Invoke();
            SetStatus(session.CurrentConnection);
            return true;
        }
        catch (Exception ex)
        {
            var result = "IRC failed: " + ex.Message;
            LogMessage(result);
            WriteSerialText("\r\n*** " + result + "\r\n");
            SendResponse("NO CARRIER");
            RecordHistory(key, preset.Server, preset.Port, result, started);
            HangUp("IRC connect failed", silent: true);
            return true;
        }
    }

'@
InsertBeforeLiteral $modemBridge '    private bool TryHandleTextService(string dialString, DateTime started)' $ircMethods 'ModemBridge IRC dial methods'

$text = N([System.IO.File]::ReadAllText($modemBridge))
if (-not $text.Contains('_ircSession?.Dispose();')) {
    $insert = @'
        try
        {
            _ircCts?.Cancel();
            _ircSession?.Dispose();
        }
        catch
        {
        }
        finally
        {
            _ircCts?.Dispose();
            _ircCts = null;
            _ircSession = null;
        }

'@
    $patchedHangup = $false

    # Most source builds use a HangUp method, but some local builds may have a different
    # visibility/signature. Match public/private/internal/protected/static variants.
    $patterns = @(
        '(?m)^(\s*(?:private|public|internal|protected)?\s*(?:static\s+)?void\s+HangUp\s*\([^)]*\)\s*\{\s*)',
        '(?m)^(\s*(?:private|public|internal|protected)?\s*(?:static\s+)?void\s+CloseConnection\s*\([^)]*\)\s*\{\s*)'
    )

    foreach ($pattern in $patterns) {
        if ([System.Text.RegularExpressions.Regex]::IsMatch($text, $pattern)) {
            $text = [System.Text.RegularExpressions.Regex]::Replace($text, $pattern, { param($m) $m.Groups[1].Value + (N($insert)) }, 1)
            $patchedHangup = $true
            break
        }
    }

    if (-not $patchedHangup) {
        # Safe fallback: do not fail the install. Patch CloseSerial so Stop Bridge cleans up IRC.
        # /quit inside IRC also disposes the IRC session, so this fallback is still usable.
        $fallbackNeedle = '        HangUp("Serial closed");'
        if ($text.Contains($fallbackNeedle)) {
            $text = $text.Replace($fallbackNeedle, $fallbackNeedle + "`n" + (N($insert)))
            $patchedHangup = $true
            Write-Host "WARNING: Could not find HangUp method signature. Patched CloseSerial IRC cleanup instead." -ForegroundColor Yellow
        }
    }

    if ($patchedHangup) {
        WriteUtf8 $modemBridge $text
        Write-Host "Patched: IRC cleanup on hangup/close" -ForegroundColor Green
    } else {
        Write-Host "WARNING: Could not find HangUp or CloseSerial patch point. Continuing. IRC still works, and /quit closes IRC from the terminal." -ForegroundColor Yellow
    }
} else {
    Write-Host "Already patched: IRC cleanup on hangup/close" -ForegroundColor DarkGray
}

ReplaceLiteral $mainForm @'
        _bridge.DialDirectory = _settings.DialDirectory;
'@ @'
        _bridge.DialDirectory = _settings.DialDirectory;
        _bridge.IrcPresets = _settings.IrcPresets;
'@ 'MainForm passes IRC presets to bridge'

ReplaceLiteral $mainForm @'
            Text = "Dial examples:\nAT\nATDT darkrealms.ca:23\nATDT1 or ATDT coco\nATDL, ATH, AT&V",
'@ @'
            Text = "Dial examples:\nAT\nATDT darkrealms.ca:23\nATDT1 or ATDT coco\nATDT IRC\nATDL, ATH, AT&V",
'@ 'MainForm IRC dial hint'

ReplaceLiteral $mainForm @'
    Text = "RetroModem Bridge v3.4";
'@ @'
    Text = "RetroModem Bridge v3.5 IRC";
'@ 'MainForm title v3.5 IRC'


# Local BBS menu integration for IRC.
# This makes IRC visible from ATDT MENU instead of requiring the user to know ATDT IRC.
$text = N([System.IO.File]::ReadAllText($modemBridge))
if (-not $text.Contains('BuildAnsiIrcInstructionsScreen')) {
    $ircScreenMethod = @'
    private string BuildAnsiIrcInstructionsScreen()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\r\n\x1b[36mRETROMODEM IRC CHAT BRIDGE\x1b[0m");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("IRC lets this retro terminal join a live text chat channel");
        sb.AppendLine("through RetroModem Bridge. Your vintage computer does not");
        sb.AppendLine("need TCP/IP, TLS, or an IRC client. RMB handles that on Windows.");
        sb.AppendLine();
        sb.AppendLine("From the main AT prompt you can dial:");
        sb.AppendLine("  ATDT IRC");
        sb.AppendLine();
        sb.AppendLine("After connecting, type messages normally and press ENTER.");
        sb.AppendLine();
        sb.AppendLine("Useful IRC commands:");
        sb.AppendLine("  /help              show IRC help");
        sb.AppendLine("  /join #channel     join another channel");
        sb.AppendLine("  /nick NewName      change nickname");
        sb.AppendLine("  /me waves          send an action");
        sb.AppendLine("  /msg nick text     private message");
        sb.AppendLine("  /quit              leave IRC");
        sb.AppendLine();
        sb.AppendLine("Default preset:");
        sb.AppendLine("  Server: irc.libera.chat");
        sb.AppendLine("  Port:   6697 TLS");
        sb.AppendLine("  Channel: #retromodem");
        sb.AppendLine();
        sb.AppendLine("Connecting now...");
        sb.AppendLine();
        return sb.ToString();
    }

'@
    if ($text.Contains('    private string BuildAnsiGoodbyeScreen()')) {
        $text = $text.Replace('    private string BuildAnsiGoodbyeScreen()', $ircScreenMethod + '    private string BuildAnsiGoodbyeScreen()')
    }
    elseif ($text.Contains('    private string BuildAnsiHelpScreen(')) {
        $text = $text.Replace('    private string BuildAnsiHelpScreen(', $ircScreenMethod + '    private string BuildAnsiHelpScreen(')
    }
    else {
        Fail "Could not find patch point for IRC local menu instruction screen in ModemBridge.cs"
    }
    WriteUtf8 $modemBridge $text
    Write-Host "Patched: local BBS IRC instruction screen" -ForegroundColor Green
} else {
    Write-Host "Already patched: local BBS IRC instruction screen" -ForegroundColor DarkGray
}

# Add IRC to the main local BBS text menu. This is intentionally conservative:
# it patches known AppendLine menu strings, and if the exact menu wording is different,
# it still adds clear IRC instructions to HELP/ATI without risking broken C# string literals.
$text = N([System.IO.File]::ReadAllText($modemBridge))
if (-not $text.Contains('IRC Chat Bridge')) {
    $patchedMenu = $false

    $menuPatches = @(
        @{ Old = 'sb.AppendLine("11. Update / Merge BBS Guide");'; New = 'sb.AppendLine("11. Update / Merge BBS Guide");' + "`n" + '        sb.AppendLine("12. IRC Chat Bridge");' },
        @{ Old = 'sb.AppendLine("[11] Update / Merge BBS Guide");'; New = 'sb.AppendLine("[11] Update / Merge BBS Guide");' + "`n" + '        sb.AppendLine("[12] IRC Chat Bridge");' },
        @{ Old = 'sb.AppendLine("11) Update / Merge BBS Guide");'; New = 'sb.AppendLine("11) Update / Merge BBS Guide");' + "`n" + '        sb.AppendLine("12) IRC Chat Bridge");' },
        @{ Old = 'sb.AppendLine("U. Update / Merge BBS Guide");'; New = 'sb.AppendLine("U. Update / Merge BBS Guide");' + "`n" + '        sb.AppendLine("I. IRC Chat Bridge");' }
    )

    foreach ($patch in $menuPatches) {
        if ($text.Contains($patch.Old)) {
            $text = $text.Replace($patch.Old, $patch.New)
            $patchedMenu = $true
            break
        }
    }

    if (-not $patchedMenu) {
        $helpNeedles = @(
            'sb.AppendLine("ATDT DOORS  - door games only");',
            'sb.AppendLine("ATDT MENU   - local RMB menu");',
            'sb.AppendLine("ATDT HELP   - this help screen");'
        )
        foreach ($needle in $helpNeedles) {
            if ($text.Contains($needle)) {
                $text = $text.Replace($needle, $needle + "`n" + '        sb.AppendLine("ATDT IRC    - IRC Chat Bridge");')
                $patchedMenu = $true
                break
            }
        }
    }

    if (-not $patchedMenu) {
        Write-Host "WARNING: Could not find the exact local menu text. IRC still works with ATDT IRC, ATI, AT&V, and the instruction screen method was added." -ForegroundColor Yellow
    } else {
        WriteUtf8 $modemBridge $text
        Write-Host "Patched: local BBS menu/help lists IRC" -ForegroundColor Green
    }
} else {
    Write-Host "Already patched: local BBS menu/help lists IRC" -ForegroundColor DarkGray
}

# Add a main-menu command that shows instructions, then connects to the default IRC preset.
$text = N([System.IO.File]::ReadAllText($modemBridge))
if (-not $text.Contains('case "IRC":')) {
    $caseBlock = @'

            case "12":
            case "I":
            case "IRC":
            case "CHAT":
            case "IRCCONNECT":
                ResetLocalBbsSession();
                WriteSerialText(BuildAnsiIrcInstructionsScreen());
                await DialAsync("IRC").ConfigureAwait(false);
                break;
'@
    $casePatched = $false
    $needles = @(
        "            default:`n                WriteSerialText(BuildAnsiErrorLine(`"Choose a menu option, M menu, or Q quit.`"));",
        "            default:`n                WriteSerialText(BuildAnsiErrorLine(`"Choose a menu option or Q quit.`"));",
        "            default:`n                WriteSerialText(BuildAnsiErrorLine("
    )
    foreach ($needle in $needles) {
        if ($text.Contains($needle)) {
            $text = $text.Replace($needle, $caseBlock + "`n" + $needle)
            $casePatched = $true
            break
        }
    }
    if (-not $casePatched) {
        # Fallback: place before the guide/update case if the switch default wording changed.
        $needle = '            case "11":'
        if ($text.Contains($needle)) {
            $text = $text.Replace($needle, $caseBlock + "`n" + $needle)
            $casePatched = $true
        }
    }
    if (-not $casePatched) { Fail "Could not find patch point for IRC local BBS menu command in ModemBridge.cs" }
    WriteUtf8 $modemBridge $text
    Write-Host "Patched: local BBS menu IRC command" -ForegroundColor Green
} else {
    Write-Host "Already patched: local BBS menu IRC command" -ForegroundColor DarkGray
}

$readme = Join-Path $ProjectRoot 'README.md'
if (Test-Path $readme) {
    $r = N([System.IO.File]::ReadAllText($readme))
    if (-not $r.Contains('IRC Chat Bridge')) {
        $r = $r.Replace('Version 3.4 also adds an experimental **Local Door Game Mode**.', 'Version 3.5 adds an experimental **IRC Chat Bridge**. Dial `ATDT IRC` from the retro terminal to join a configured IRC server and channel. Version 3.4 also adds an experimental **Local Door Game Mode**.')
        $r = $r.Replace('- Local Door Game Mode with `DOOR32.SYS` generation.', '- Local Door Game Mode with `DOOR32.SYS` generation.' + "`n" + '- IRC Chat Bridge with TLS, presets, PING/PONG handling, nickname fallback, plain-text chat, and simple slash commands.')
        WriteUtf8 $readme $r
        Write-Host "Patched: README IRC notes" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "IRC feature installed into source. Installer v3.5.5." -ForegroundColor Cyan
Write-Host "Dial from retro terminal: ATDT IRC" -ForegroundColor Cyan
Write-Host "IRC commands: /help, /join #channel, /nick name, /me action, /msg nick text, /quit" -ForegroundColor Cyan

if ($Build) {
    Push-Location $ProjectRoot
    try {
        dotnet build .\RetroModemBridge\RetroModemBridge.csproj
        dotnet publish .\RetroModemBridge\RetroModemBridge.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\publish\v3.5-irc
    }
    finally {
        Pop-Location
    }
}
