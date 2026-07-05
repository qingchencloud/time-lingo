param(
    [string]$Provider = "",
    [string]$MicrosoftKey = "",
    [string]$MicrosoftRegion = "",
    [string]$MicrosoftEndpoint = "",
    [string]$DeepLKey = "",
    [string]$DeepLUrl = "",
    [string]$MyMemoryEndpoint = "",
    [switch]$WorkerMode,
    [string]$InputFile = "",
    [string]$OutputFile = "",
    [string]$WorkerDirection = "",
    [switch]$InstallShortcut,
    [switch]$EnableAutoStart,
    [switch]$DisableAutoStart
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

if ([string]::IsNullOrWhiteSpace($Provider)) {
    $Provider = if ($env:RED_FRAME_TRANSLATOR_PROVIDER) { $env:RED_FRAME_TRANSLATOR_PROVIDER } else { "mymemory" }
}
if ([string]::IsNullOrWhiteSpace($MicrosoftKey)) {
    $MicrosoftKey = $env:RED_FRAME_TRANSLATOR_MICROSOFT_KEY
}
if ([string]::IsNullOrWhiteSpace($MicrosoftRegion)) {
    $MicrosoftRegion = $env:RED_FRAME_TRANSLATOR_MICROSOFT_REGION
}
if ([string]::IsNullOrWhiteSpace($MicrosoftEndpoint)) {
    $MicrosoftEndpoint = if ($env:RED_FRAME_TRANSLATOR_MICROSOFT_ENDPOINT) { $env:RED_FRAME_TRANSLATOR_MICROSOFT_ENDPOINT } else { "https://api.cognitive.microsofttranslator.com" }
}
if ([string]::IsNullOrWhiteSpace($DeepLKey)) {
    $DeepLKey = if ($env:RED_FRAME_TRANSLATOR_DEEPL_KEY) { $env:RED_FRAME_TRANSLATOR_DEEPL_KEY } else { $env:DEEPL_AUTH_KEY }
}
if ([string]::IsNullOrWhiteSpace($DeepLUrl)) {
    $DeepLUrl = if ($env:RED_FRAME_TRANSLATOR_DEEPL_URL) { $env:RED_FRAME_TRANSLATOR_DEEPL_URL } else { "https://api-free.deepl.com/v2/translate" }
}
if ([string]::IsNullOrWhiteSpace($MyMemoryEndpoint)) {
    $MyMemoryEndpoint = if ($env:RED_FRAME_TRANSLATOR_MYMEMORY_ENDPOINT) { $env:RED_FRAME_TRANSLATOR_MYMEMORY_ENDPOINT } else { "https://api.mymemory.translated.net/get" }
}

$script:Provider = $Provider.Trim().ToLowerInvariant()
$script:MicrosoftKey = $MicrosoftKey
$script:MicrosoftRegion = $MicrosoftRegion
$script:MicrosoftEndpoint = $MicrosoftEndpoint.TrimEnd("/")
$script:DeepLKey = $DeepLKey
$script:DeepLUrl = $DeepLUrl
$script:MyMemoryEndpoint = $MyMemoryEndpoint
$script:IsDarkTheme = $false
$script:AllowExit = $false
$script:ScriptPath = if ($PSCommandPath) { $PSCommandPath } else { $MyInvocation.MyCommand.Path }
$script:ToolDir = Split-Path -Parent $script:ScriptPath
$script:IconPath = Join-Path $script:ToolDir "beijing-translator.ico"
$script:PowerShellExe = try {
    $processPath = (Get-Process -Id $PID).Path
    if ([string]::IsNullOrWhiteSpace($processPath)) { "powershell.exe" } else { $processPath }
}
catch {
    "powershell.exe"
}

function New-AppIconFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ((Test-Path -LiteralPath $Path) -and ((Get-Item -LiteralPath $Path).Length -gt 0)) {
        return
    }

    $bitmap = New-Object System.Drawing.Bitmap 64, 64
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    $backgroundRect = New-Object System.Drawing.Rectangle 0, 0, 64, 64
    $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $backgroundRect,
        ([System.Drawing.Color]::FromArgb(37, 99, 235)),
        ([System.Drawing.Color]::FromArgb(20, 184, 166)),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $bluePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(37, 99, 235)), 4
    $darkPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(15, 23, 42)), 2
    $smallBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(15, 23, 42))
    $font = New-Object System.Drawing.Font "Microsoft YaHei UI", 12, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillEllipse($backgroundBrush, 3, 3, 58, 58)
        $graphics.FillEllipse($whiteBrush, 13, 11, 38, 38)
        $graphics.DrawEllipse($darkPen, 13, 11, 38, 38)
        $graphics.DrawLine($bluePen, 32, 30, 32, 18)
        $graphics.DrawLine($bluePen, 32, 30, 43, 35)
        $graphics.FillRectangle($smallBrush, 36, 40, 22, 16)
        $graphics.DrawString("中", $font, $whiteBrush, 40, 41)

        $icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
        try {
            $icon.Save($stream)
        }
        finally {
            $stream.Dispose()
            $icon.Dispose()
        }
    }
    finally {
        $font.Dispose()
        $smallBrush.Dispose()
        $darkPen.Dispose()
        $bluePen.Dispose()
        $whiteBrush.Dispose()
        $backgroundBrush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Get-AppIcon {
    try {
        New-AppIconFile -Path $script:IconPath
        return New-Object System.Drawing.Icon $script:IconPath
    }
    catch {
        return [System.Drawing.SystemIcons]::Application
    }
}

function Get-LauncherPath {
    return Join-Path $script:ToolDir "Start-ClockTranslator.vbs"
}

function Get-WScriptPath {
    return Join-Path $env:WINDIR "System32\wscript.exe"
}

function Save-WindowsShortcut {
    param(
        [Parameter(Mandatory = $true)][string]$ShortcutPath,
        [Parameter(Mandatory = $true)][string]$TargetPath,
        [string]$Arguments = "",
        [string]$WorkingDirectory = "",
        [string]$IconLocation = "",
        [string]$Description = ""
    )

    $shortcutDir = Split-Path -Parent $ShortcutPath
    if (-not (Test-Path -LiteralPath $shortcutDir)) {
        [System.IO.Directory]::CreateDirectory($shortcutDir) | Out-Null
    }
    $tempShortcutPath = Join-Path $shortcutDir ("RedFrameShortcut-" + [System.Guid]::NewGuid().ToString("N") + ".lnk")
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($tempShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.Arguments = $Arguments
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.IconLocation = $IconLocation
    $shortcut.Description = $Description
    $shortcut.Save()

    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }
    [System.IO.File]::Move($tempShortcutPath, $ShortcutPath)
}

function Install-DesktopShortcut {
    New-AppIconFile -Path $script:IconPath

    $desktop = [System.Environment]::GetFolderPath("DesktopDirectory")
    $shortcutPath = Join-Path $desktop "北京时间翻译助手.lnk"
    Save-WindowsShortcut `
        -ShortcutPath $shortcutPath `
        -TargetPath (Get-WScriptPath) `
        -Arguments ('"' + (Get-LauncherPath) + '"') `
        -WorkingDirectory $script:ToolDir `
        -IconLocation $script:IconPath `
        -Description "北京时间翻译助手"

    return $shortcutPath
}

function Get-StartupShortcutPath {
    $startup = [System.Environment]::GetFolderPath("Startup")
    return Join-Path $startup "BeijingTimeTranslator.lnk"
}

function Test-AutoStartEnabled {
    return (Test-Path -LiteralPath (Get-StartupShortcutPath))
}

function Set-AutoStartEnabled {
    param([Parameter(Mandatory = $true)][bool]$Enabled)

    $shortcutPath = Get-StartupShortcutPath
    if ($Enabled) {
        New-AppIconFile -Path $script:IconPath
        Save-WindowsShortcut `
            -ShortcutPath $shortcutPath `
            -TargetPath (Get-WScriptPath) `
            -Arguments ('"' + (Get-LauncherPath) + '"') `
            -WorkingDirectory $script:ToolDir `
            -IconLocation $script:IconPath `
            -Description "北京时间翻译助手"
        return
    }

    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }
}

if ($InstallShortcut) {
    $shortcutPath = Install-DesktopShortcut
    Write-Output "已创建桌面图标：$shortcutPath"
    exit 0
}

if ($EnableAutoStart) {
    Set-AutoStartEnabled -Enabled $true
    Write-Output "已开启开机自启：$(Get-StartupShortcutPath)"
    exit 0
}

if ($DisableAutoStart) {
    Set-AutoStartEnabled -Enabled $false
    Write-Output "已关闭开机自启：$(Get-StartupShortcutPath)"
    exit 0
}

function Join-ApiUrl {
    param(
        [Parameter(Mandatory = $true)][string]$Base,
        [Parameter(Mandatory = $true)][string]$Path
    )

    if ($Path -match "^https?://") {
        return $Path
    }

    if (-not $Path.StartsWith("/")) {
        $Path = "/" + $Path
    }

    return $Base.TrimEnd("/") + $Path
}

function Get-BeijingNow {
    try {
        $timezone = [System.TimeZoneInfo]::FindSystemTimeZoneById("China Standard Time")
        return [System.TimeZoneInfo]::ConvertTimeFromUtc([System.DateTime]::UtcNow, $timezone)
    }
    catch {
        return [System.DateTime]::UtcNow.AddHours(8)
    }
}

function Test-ProbablyChinese {
    param([Parameter(Mandatory = $true)][string]$Text)
    return ($Text -match "\p{IsCJKUnifiedIdeographs}")
}

function Test-TechnicalLine {
    param([Parameter(Mandatory = $true)][string]$Text)

    $trimmed = $Text.Trim()
    if ($trimmed -eq "") {
        return $false
    }
    if ($trimmed -match "^[A-Za-z]:\\|^\\\\|/mnt/|/home/|/usr/|/var/|/etc/") {
        return $true
    }
    if ($trimmed -match "\\|/|--|&&|\|\||\.(ps1|cmd|bat|exe|dll|json|md|go|rs|ts|tsx|js|jsx|vue|toml|yaml|yml)$") {
        return $true
    }
    if ($trimmed -match "^(cd|dir|ls|pwd|git|npm|pnpm|yarn|npx|node|go|cargo|rustup|python|python3|pip|pip3|docker|docker-compose|kubectl|ssh|scp|curl|wget|powershell|pwsh|cmd|make|cmake|dotnet|deno|bun|wrangler|vercel|netlify|gh|rg|grep)\b") {
        return $true
    }
    return $false
}

function Resolve-TranslationDirection {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Direction
    )

    if ($Direction -eq "中文转英文") {
        return [pscustomobject]@{ Source = "zh-CN"; Target = "en"; MicrosoftSource = "zh-Hans"; MicrosoftTarget = "en"; DeepLSource = "ZH"; DeepLTarget = "EN-US"; Label = "中文转英文" }
    }
    if ($Direction -eq "英文转中文") {
        return [pscustomobject]@{ Source = "en"; Target = "zh-CN"; MicrosoftSource = "en"; MicrosoftTarget = "zh-Hans"; DeepLSource = "EN"; DeepLTarget = "ZH-HANS"; Label = "英文转中文" }
    }
    if (Test-ProbablyChinese -Text $Text) {
        return [pscustomobject]@{ Source = "zh-CN"; Target = "en"; MicrosoftSource = "zh-Hans"; MicrosoftTarget = "en"; DeepLSource = "ZH"; DeepLTarget = "EN-US"; Label = "中文转英文" }
    }
    return [pscustomobject]@{ Source = "en"; Target = "zh-CN"; MicrosoftSource = "en"; MicrosoftTarget = "zh-Hans"; DeepLSource = "EN"; DeepLTarget = "ZH-HANS"; Label = "英文转中文" }
}

function Invoke-MyMemoryTranslate {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)]$DirectionInfo
    )

    $parts = Split-TextForTranslation -Text $Text -MaxLength 260
    $translated = New-Object System.Collections.Generic.List[string]
    foreach ($part in $parts) {
        if ([string]::IsNullOrWhiteSpace($part)) {
            $translated.Add($part)
            continue
        }
        if (Test-TechnicalLine -Text $part) {
            $translated.Add($part)
            continue
        }
        $translated.Add((Invoke-MyMemoryTranslateSingle -Text $part -DirectionInfo $DirectionInfo))
        Start-Sleep -Milliseconds 120
    }
    return ([string]::Join("", $translated)).Trim()
}

function Split-TextForTranslation {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [int]$MaxLength = 260
    )

    $parts = New-Object System.Collections.Generic.List[string]
    $linePieces = [regex]::Split($Text, "(\r\n|\n|\r)")
    foreach ($piece in $linePieces) {
        if ($piece -match "^\r\n$|^\n$|^\r$") {
            $parts.Add($piece)
            continue
        }

        if ($piece.Length -le $MaxLength) {
            $parts.Add($piece)
            continue
        }

        $start = 0
        while ($start -lt $piece.Length) {
            $length = [Math]::Min($MaxLength, $piece.Length - $start)
            $chunk = $piece.Substring($start, $length)

            if (($start + $length) -lt $piece.Length) {
                $cut = $chunk.LastIndexOfAny(([char[]]@(" ", "，", "。", "；", "、", ",", ".", ";", ":", "：")))
                if ($cut -gt 80) {
                    $length = $cut + 1
                    $chunk = $piece.Substring($start, $length)
                }
            }

            $parts.Add($chunk)
            $start += $length
        }
    }
    return $parts
}

function Invoke-MyMemoryTranslateSingle {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)]$DirectionInfo
    )

    $encodedText = [System.Uri]::EscapeDataString($Text)
    $langPair = [System.Uri]::EscapeDataString($DirectionInfo.Source + "|" + $DirectionInfo.Target)
    $uri = $script:MyMemoryEndpoint + "?q=$encodedText&langpair=$langPair"
    $response = Invoke-RestMethod -Method Get -Uri $uri -TimeoutSec 20
    if ($response.responseStatus -and [int]$response.responseStatus -ge 400) {
        throw $response.responseDetails
    }
    if (-not $response.responseData -or [string]::IsNullOrWhiteSpace($response.responseData.translatedText)) {
        throw "翻译接口没有返回结果。"
    }
    return ([string]$response.responseData.translatedText).Trim()
}

function Invoke-MicrosoftTranslate {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)]$DirectionInfo
    )

    if ([string]::IsNullOrWhiteSpace($script:MicrosoftKey)) {
        throw "请先配置 Microsoft Translator 密钥：RED_FRAME_TRANSLATOR_MICROSOFT_KEY。"
    }

    $uri = $script:MicrosoftEndpoint + "/translate?api-version=3.0&from=$($DirectionInfo.MicrosoftSource)&to=$($DirectionInfo.MicrosoftTarget)"
    $headers = @{
        "Ocp-Apim-Subscription-Key" = $script:MicrosoftKey
        "Content-Type" = "application/json; charset=utf-8"
    }
    if (-not [string]::IsNullOrWhiteSpace($script:MicrosoftRegion)) {
        $headers["Ocp-Apim-Subscription-Region"] = $script:MicrosoftRegion
    }

    $body = @(@{ Text = $Text }) | ConvertTo-Json -Depth 4
    $response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $body -TimeoutSec 20
    if (-not $response[0].translations -or [string]::IsNullOrWhiteSpace($response[0].translations[0].text)) {
        throw "Microsoft Translator 没有返回结果。"
    }
    return ([string]$response[0].translations[0].text).Trim()
}

function Invoke-DeepLTranslate {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)]$DirectionInfo
    )

    if ([string]::IsNullOrWhiteSpace($script:DeepLKey)) {
        throw "请先配置 DeepL API Key：RED_FRAME_TRANSLATOR_DEEPL_KEY 或 DEEPL_AUTH_KEY。"
    }

    $headers = @{ "Authorization" = "DeepL-Auth-Key " + $script:DeepLKey }
    $body = @{
        text = $Text
        source_lang = $DirectionInfo.DeepLSource
        target_lang = $DirectionInfo.DeepLTarget
    }
    $response = Invoke-RestMethod -Method Post -Uri $script:DeepLUrl -Headers $headers -Body $body -TimeoutSec 20
    if (-not $response.translations -or [string]::IsNullOrWhiteSpace($response.translations[0].text)) {
        throw "DeepL 没有返回结果。"
    }
    return ([string]$response.translations[0].text).Trim()
}

function Invoke-TranslationApi {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Direction
    )

    $directionInfo = Resolve-TranslationDirection -Text $Text -Direction $Direction
    switch ($script:Provider) {
        "microsoft" { return Invoke-MicrosoftTranslate -Text $Text -DirectionInfo $directionInfo }
        "deepl" { return Invoke-DeepLTranslate -Text $Text -DirectionInfo $directionInfo }
        "mymemory" { return Invoke-MyMemoryTranslate -Text $Text -DirectionInfo $directionInfo }
        default { throw "未知翻译接口：$($script:Provider)。可用值：mymemory、microsoft、deepl。" }
    }
}

function Write-WorkerResult {
    param(
        [Parameter(Mandatory = $true)][bool]$Ok,
        [string]$Text = "",
        [string]$ErrorMessage = ""
    )

    if ([string]::IsNullOrWhiteSpace($OutputFile)) {
        throw "缺少输出文件路径。"
    }

    $payload = [pscustomobject]@{
        ok = $Ok
        text = $Text
        error = $ErrorMessage
    }
    $json = $payload | ConvertTo-Json -Compress
    $utf8NoBom = New-Object System.Text.UTF8Encoding -ArgumentList $false
    $tempOutput = $OutputFile + ".tmp"
    [System.IO.File]::WriteAllText($tempOutput, $json, $utf8NoBom)
    Move-Item -LiteralPath $tempOutput -Destination $OutputFile -Force
}

if ($WorkerMode) {
    try {
        if ([string]::IsNullOrWhiteSpace($InputFile) -or -not (Test-Path -LiteralPath $InputFile)) {
            throw "缺少输入内容。"
        }
        $utf8NoBom = New-Object System.Text.UTF8Encoding -ArgumentList $false
        $workerText = [System.IO.File]::ReadAllText($InputFile, $utf8NoBom)
        $direction = if ([string]::IsNullOrWhiteSpace($WorkerDirection)) { "自动判断" } else { $WorkerDirection }
        $translatedText = Invoke-TranslationApi -Text $workerText -Direction $direction
        Write-WorkerResult -Ok $true -Text $translatedText
        exit 0
    }
    catch {
        Write-WorkerResult -Ok $false -ErrorMessage $_.Exception.Message
        exit 1
    }
}

function New-UiFont {
    param(
        [float]$Size,
        [System.Drawing.FontStyle]$Style = [System.Drawing.FontStyle]::Regular
    )
    return New-Object System.Drawing.Font("Microsoft YaHei UI", $Size, $Style)
}

function New-UiColor {
    param(
        [int]$R,
        [int]$G,
        [int]$B
    )
    return [System.Drawing.Color]::FromArgb($R, $G, $B)
}

function Get-ThemeColor {
    param([Parameter(Mandatory = $true)][string]$Name)

    if ($script:IsDarkTheme) {
        switch ($Name) {
            "Background" { return New-UiColor -R 15 -G 23 -B 42 }
            "Surface" { return New-UiColor -R 30 -G 41 -B 59 }
            "SurfaceAlt" { return New-UiColor -R 17 -G 24 -B 39 }
            "Text" { return New-UiColor -R 248 -G 250 -B 252 }
            "MutedText" { return New-UiColor -R 203 -G 213 -B 225 }
            "Border" { return New-UiColor -R 71 -G 85 -B 105 }
            "Primary" { return New-UiColor -R 59 -G 130 -B 246 }
            "PrimaryHover" { return New-UiColor -R 37 -G 99 -B 235 }
            "PrimaryDown" { return New-UiColor -R 29 -G 78 -B 216 }
            "Button" { return New-UiColor -R 30 -G 41 -B 59 }
            "ButtonHover" { return New-UiColor -R 51 -G 65 -B 85 }
            "ButtonDown" { return New-UiColor -R 71 -G 85 -B 105 }
            "Danger" { return New-UiColor -R 248 -G 113 -B 113 }
            "Warn" { return New-UiColor -R 251 -G 146 -B 60 }
        }
    }

    switch ($Name) {
        "Background" { return New-UiColor -R 248 -G 250 -B 252 }
        "Surface" { return [System.Drawing.Color]::White }
        "SurfaceAlt" { return New-UiColor -R 241 -G 245 -B 249 }
        "Text" { return New-UiColor -R 15 -G 23 -B 42 }
        "MutedText" { return New-UiColor -R 71 -G 85 -B 105 }
        "Border" { return New-UiColor -R 203 -G 213 -B 225 }
        "Primary" { return New-UiColor -R 37 -G 99 -B 235 }
        "PrimaryHover" { return New-UiColor -R 29 -G 78 -B 216 }
        "PrimaryDown" { return New-UiColor -R 30 -G 64 -B 175 }
        "Button" { return [System.Drawing.Color]::White }
        "ButtonHover" { return New-UiColor -R 241 -G 245 -B 249 }
        "ButtonDown" { return New-UiColor -R 226 -G 232 -B 240 }
        "Danger" { return New-UiColor -R 220 -G 38 -B 38 }
        "Warn" { return New-UiColor -R 234 -G 88 -B 12 }
    }
}

function New-DarkLabel {
    param(
        [string]$Text,
        [float]$Size = 9,
        [System.Drawing.FontStyle]$Style = [System.Drawing.FontStyle]::Regular
    )
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $Text
    $label.AutoSize = $false
    $label.Dock = "Fill"
    $label.ForeColor = Get-ThemeColor -Name "Text"
    $label.Font = New-UiFont -Size $Size -Style $Style
    $label.TextAlign = [System.Drawing.ContentAlignment]::MiddleLeft
    return $label
}

function New-DarkButton {
    param([string]$Text)
    $button = New-Object System.Windows.Forms.Button
    $button.Text = $Text
    $button.FlatStyle = "Flat"
    $button.Height = 36
    $button.BackColor = Get-ThemeColor -Name "Button"
    $button.ForeColor = Get-ThemeColor -Name "Text"
    $button.Font = New-UiFont -Size 9 -Style ([System.Drawing.FontStyle]::Regular)
    $button.Cursor = [System.Windows.Forms.Cursors]::Hand
    $button.FlatAppearance.BorderColor = Get-ThemeColor -Name "Border"
    $button.FlatAppearance.MouseOverBackColor = Get-ThemeColor -Name "ButtonHover"
    $button.FlatAppearance.MouseDownBackColor = Get-ThemeColor -Name "ButtonDown"
    return $button
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "北京时间翻译助手"
$form.Size = New-Object System.Drawing.Size(460, 520)
$form.MinimumSize = New-Object System.Drawing.Size(420, 460)
$form.StartPosition = "Manual"
$form.TopMost = $true
$form.BackColor = Get-ThemeColor -Name "Background"
$form.ForeColor = Get-ThemeColor -Name "Text"
$form.Font = New-UiFont -Size 9
$script:AppIcon = Get-AppIcon
$form.Icon = $script:AppIcon

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$form.Location = New-Object System.Drawing.Point(($screen.Right - $form.Width - 24), ($screen.Top + 48))

$layout = New-Object System.Windows.Forms.TableLayoutPanel
$layout.Dock = "Fill"
$layout.ColumnCount = 1
$layout.RowCount = 7
$layout.Padding = New-Object System.Windows.Forms.Padding(12)
$layout.BackColor = $form.BackColor
$layout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 72))) | Out-Null
$layout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 38))) | Out-Null
$layout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 22))) | Out-Null
$layout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent, 45))) | Out-Null
$layout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 22))) | Out-Null
$layout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent, 55))) | Out-Null
$layout.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 30))) | Out-Null
$form.Controls.Add($layout)

$header = New-Object System.Windows.Forms.TableLayoutPanel
$header.Dock = "Fill"
$header.ColumnCount = 2
$header.RowCount = 2
$header.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100))) | Out-Null
$header.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 224))) | Out-Null
$header.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent, 64))) | Out-Null
$header.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent, 36))) | Out-Null
$header.BackColor = $form.BackColor

$timeLabel = New-DarkLabel -Text "" -Size 22 -Style ([System.Drawing.FontStyle]::Bold)
$dateLabel = New-DarkLabel -Text "北京时间 UTC+8" -Size 9

$switchPanel = New-Object System.Windows.Forms.FlowLayoutPanel
$switchPanel.Dock = "Fill"
$switchPanel.FlowDirection = [System.Windows.Forms.FlowDirection]::RightToLeft
$switchPanel.WrapContents = $false
$switchPanel.BackColor = $form.BackColor

$topMostCheck = New-Object System.Windows.Forms.CheckBox
$topMostCheck.Text = "置顶"
$topMostCheck.Checked = $true
$topMostCheck.AutoSize = $true
$topMostCheck.Margin = New-Object System.Windows.Forms.Padding(4, 8, 0, 0)
$topMostCheck.ForeColor = Get-ThemeColor -Name "MutedText"
$topMostCheck.BackColor = $form.BackColor
$topMostCheck.Font = New-UiFont -Size 9

$trayCheck = New-Object System.Windows.Forms.CheckBox
$trayCheck.Text = "托盘"
$trayCheck.Checked = $true
$trayCheck.AutoSize = $true
$trayCheck.Margin = New-Object System.Windows.Forms.Padding(4, 8, 0, 0)
$trayCheck.ForeColor = Get-ThemeColor -Name "MutedText"
$trayCheck.BackColor = $form.BackColor
$trayCheck.Font = New-UiFont -Size 9

$themeCheck = New-Object System.Windows.Forms.CheckBox
$themeCheck.Text = "夜间"
$themeCheck.Checked = $false
$themeCheck.AutoSize = $true
$themeCheck.Margin = New-Object System.Windows.Forms.Padding(4, 8, 0, 0)
$themeCheck.ForeColor = Get-ThemeColor -Name "MutedText"
$themeCheck.BackColor = $form.BackColor
$themeCheck.Font = New-UiFont -Size 9

$autoStartCheck = New-Object System.Windows.Forms.CheckBox
$autoStartCheck.Text = "自启"
$autoStartCheck.Checked = Test-AutoStartEnabled
$autoStartCheck.AutoSize = $true
$autoStartCheck.Margin = New-Object System.Windows.Forms.Padding(4, 8, 0, 0)
$autoStartCheck.ForeColor = Get-ThemeColor -Name "MutedText"
$autoStartCheck.BackColor = $form.BackColor
$autoStartCheck.Font = New-UiFont -Size 9

$switchPanel.Controls.Add($themeCheck)
$switchPanel.Controls.Add($trayCheck)
$switchPanel.Controls.Add($topMostCheck)
$switchPanel.Controls.Add($autoStartCheck)

$endpointLabel = New-DarkLabel -Text "在线翻译接口" -Size 8
$endpointLabel.ForeColor = Get-ThemeColor -Name "MutedText"
$endpointLabel.TextAlign = [System.Drawing.ContentAlignment]::MiddleRight

$header.Controls.Add($timeLabel, 0, 0)
$header.Controls.Add($switchPanel, 1, 0)
$header.Controls.Add($dateLabel, 0, 1)
$header.Controls.Add($endpointLabel, 1, 1)
$layout.Controls.Add($header, 0, 0)

$toolbar = New-Object System.Windows.Forms.TableLayoutPanel
$toolbar.Dock = "Fill"
$toolbar.ColumnCount = 4
$toolbar.RowCount = 1
$toolbar.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100))) | Out-Null
$toolbar.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 88))) | Out-Null
$toolbar.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 88))) | Out-Null
$toolbar.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 64))) | Out-Null
$toolbar.BackColor = $form.BackColor

$directionBox = New-Object System.Windows.Forms.ComboBox
$directionBox.DropDownStyle = "DropDownList"
$directionBox.Items.AddRange(@("自动判断", "中文转英文", "英文转中文"))
$directionBox.SelectedIndex = 0
$directionBox.Dock = "Fill"
$directionBox.Font = New-UiFont -Size 9

$translateButton = New-DarkButton -Text "开始翻译"
$copyButton = New-DarkButton -Text "复制结果"
$clearButton = New-DarkButton -Text "清空"

$toolbar.Controls.Add($directionBox, 0, 0)
$toolbar.Controls.Add($translateButton, 1, 0)
$toolbar.Controls.Add($copyButton, 2, 0)
$toolbar.Controls.Add($clearButton, 3, 0)
$layout.Controls.Add($toolbar, 0, 1)

$inputLabel = New-DarkLabel -Text "输入内容" -Size 9 -Style ([System.Drawing.FontStyle]::Bold)
$outputLabel = New-DarkLabel -Text "翻译结果" -Size 9 -Style ([System.Drawing.FontStyle]::Bold)
$layout.Controls.Add($inputLabel, 0, 2)

$inputBox = New-Object System.Windows.Forms.TextBox
$inputBox.Multiline = $true
$inputBox.AcceptsReturn = $true
$inputBox.AcceptsTab = $true
$inputBox.ScrollBars = "Vertical"
$inputBox.Dock = "Fill"
$inputBox.BackColor = [System.Drawing.Color]::White
$inputBox.ForeColor = New-UiColor -R 15 -G 23 -B 42
$inputBox.BorderStyle = "FixedSingle"
$inputBox.Font = New-UiFont -Size 10
$layout.Controls.Add($inputBox, 0, 3)

$layout.Controls.Add($outputLabel, 0, 4)

$outputBox = New-Object System.Windows.Forms.TextBox
$outputBox.Multiline = $true
$outputBox.AcceptsReturn = $true
$outputBox.ReadOnly = $true
$outputBox.ScrollBars = "Vertical"
$outputBox.Dock = "Fill"
$outputBox.BackColor = New-UiColor -R 241 -G 245 -B 249
$outputBox.ForeColor = New-UiColor -R 15 -G 23 -B 42
$outputBox.BorderStyle = "FixedSingle"
$outputBox.Font = New-UiFont -Size 10
$layout.Controls.Add($outputBox, 0, 5)

$statusLabel = New-DarkLabel -Text "准备好了。" -Size 8
$statusLabel.ForeColor = Get-ThemeColor -Name "MutedText"
$layout.Controls.Add($statusLabel, 0, 6)

function Set-ButtonTheme {
    param(
        [Parameter(Mandatory = $true)]$Button,
        [bool]$Primary = $false
    )

    if ($Primary) {
        $Button.BackColor = Get-ThemeColor -Name "Primary"
        $Button.ForeColor = [System.Drawing.Color]::White
        $Button.FlatAppearance.BorderColor = Get-ThemeColor -Name "Primary"
        $Button.FlatAppearance.MouseOverBackColor = Get-ThemeColor -Name "PrimaryHover"
        $Button.FlatAppearance.MouseDownBackColor = Get-ThemeColor -Name "PrimaryDown"
        return
    }

    $Button.BackColor = Get-ThemeColor -Name "Button"
    $Button.ForeColor = Get-ThemeColor -Name "Text"
    $Button.FlatAppearance.BorderColor = Get-ThemeColor -Name "Border"
    $Button.FlatAppearance.MouseOverBackColor = Get-ThemeColor -Name "ButtonHover"
    $Button.FlatAppearance.MouseDownBackColor = Get-ThemeColor -Name "ButtonDown"
}

function Apply-Theme {
    $background = Get-ThemeColor -Name "Background"
    $surface = Get-ThemeColor -Name "Surface"
    $surfaceAlt = Get-ThemeColor -Name "SurfaceAlt"
    $text = Get-ThemeColor -Name "Text"
    $muted = Get-ThemeColor -Name "MutedText"
    $border = Get-ThemeColor -Name "Border"

    $form.BackColor = $background
    $form.ForeColor = $text
    $layout.BackColor = $background
    $header.BackColor = $background
    $switchPanel.BackColor = $background
    $toolbar.BackColor = $background

    foreach ($label in @($timeLabel, $dateLabel, $inputLabel, $outputLabel)) {
        $label.ForeColor = $text
        $label.BackColor = $background
    }
    foreach ($label in @($endpointLabel, $statusLabel)) {
        $label.ForeColor = $muted
        $label.BackColor = $background
    }
    foreach ($check in @($topMostCheck, $trayCheck, $themeCheck, $autoStartCheck)) {
        $check.ForeColor = $muted
        $check.BackColor = $background
    }

    $directionBox.BackColor = $surface
    $directionBox.ForeColor = $text

    $inputBox.BackColor = $surface
    $inputBox.ForeColor = $text
    $outputBox.BackColor = $surfaceAlt
    $outputBox.ForeColor = $text

    Set-ButtonTheme -Button $translateButton -Primary $true
    Set-ButtonTheme -Button $copyButton
    Set-ButtonTheme -Button $clearButton

    $form.Refresh()
}

$trayMenu = New-Object System.Windows.Forms.ContextMenuStrip
$showTrayItem = New-Object System.Windows.Forms.ToolStripMenuItem("显示")
$exitTrayItem = New-Object System.Windows.Forms.ToolStripMenuItem("退出")
[void]$trayMenu.Items.Add($showTrayItem)
[void]$trayMenu.Items.Add($exitTrayItem)

$notifyIcon = New-Object System.Windows.Forms.NotifyIcon
$notifyIcon.Text = "北京时间翻译助手"
$notifyIcon.Icon = $script:AppIcon
$notifyIcon.ContextMenuStrip = $trayMenu
$notifyIcon.Visible = $true

function Show-MainWindow {
    $form.Show()
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.Activate()
}

$showTrayItem.Add_Click({ Show-MainWindow })
$notifyIcon.Add_DoubleClick({ Show-MainWindow })
$exitTrayItem.Add_Click({
    $script:AllowExit = $true
    $notifyIcon.Visible = $false
    $form.Close()
})

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 1000
$timer.Add_Tick({
    $now = Get-BeijingNow
    $timeLabel.Text = $now.ToString("HH:mm:ss")
    $dateLabel.Text = "北京时间 UTC+8  " + $now.ToString("yyyy-MM-dd ddd")
})
$timer.Start()

$topMostCheck.Add_CheckedChanged({
    $form.TopMost = $topMostCheck.Checked
})

$trayCheck.Add_CheckedChanged({
    $notifyIcon.Visible = $trayCheck.Checked
})

$themeCheck.Add_CheckedChanged({
    $script:IsDarkTheme = $themeCheck.Checked
    Apply-Theme
})

$script:UpdatingAutoStart = $false
$autoStartCheck.Add_CheckedChanged({
    if ($script:UpdatingAutoStart) {
        return
    }

    try {
        Set-AutoStartEnabled -Enabled $autoStartCheck.Checked
        $statusLabel.ForeColor = Get-ThemeColor -Name "MutedText"
        if ($autoStartCheck.Checked) {
            $statusLabel.Text = "开机自启已开启。"
        }
        else {
            $statusLabel.Text = "开机自启已关闭。"
        }
    }
    catch {
        $statusLabel.ForeColor = Get-ThemeColor -Name "Danger"
        $statusLabel.Text = "自启设置失败：" + $_.Exception.Message
        $script:UpdatingAutoStart = $true
        $autoStartCheck.Checked = Test-AutoStartEnabled
        $script:UpdatingAutoStart = $false
    }
})

$form.Add_FormClosing({
    param($sender, $eventArgs)
    if (-not $script:AllowExit -and $trayCheck.Checked) {
        $eventArgs.Cancel = $true
        $form.Hide()
        $notifyIcon.Visible = $true
        $notifyIcon.ShowBalloonTip(1200, "北京时间翻译助手", "已到托盘，右键可退出。", [System.Windows.Forms.ToolTipIcon]::Info)
    }
})

$form.Add_FormClosed({
    if ($script:ActiveTranslation -and $script:ActiveTranslation.Process -and -not $script:ActiveTranslation.Process.HasExited) {
        try {
            $script:ActiveTranslation.Process.Kill()
        }
        catch {
        }
    }
    if ($script:ActiveTranslation) {
        Remove-TranslationJobFiles -Job $script:ActiveTranslation
        $script:ActiveTranslation = $null
    }
    $notifyIcon.Visible = $false
    $notifyIcon.Icon = $null
    $notifyIcon.Dispose()
    if ($script:AppIcon) {
        $script:AppIcon.Dispose()
    }
})

function Set-TranslationBusy {
    param([bool]$Busy)

    $translateButton.Enabled = -not $Busy
    $copyButton.Enabled = -not $Busy
    $clearButton.Enabled = -not $Busy
    $directionBox.Enabled = -not $Busy
}

function Format-ProcessArgument {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }
    return '"' + $Value.Replace('"', '\"') + '"'
}

function Remove-TranslationJobFiles {
    param($Job)

    foreach ($path in @($Job.InputFile, $Job.OutputFile, ($Job.OutputFile + ".tmp"))) {
        if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path -LiteralPath $path)) {
            try {
                Remove-Item -LiteralPath $path -Force
            }
            catch {
            }
        }
    }
}

$script:ActiveTranslation = $null

function Start-TranslationJob {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Direction
    )

    if ($script:ActiveTranslation) {
        return
    }

    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "RedFrameClockTranslator"
    [System.IO.Directory]::CreateDirectory($tempDir) | Out-Null
    $jobId = [System.Guid]::NewGuid().ToString("N")
    $inputFile = Join-Path $tempDir ($jobId + ".input.txt")
    $outputFile = Join-Path $tempDir ($jobId + ".output.json")
    $utf8NoBom = New-Object System.Text.UTF8Encoding -ArgumentList $false
    [System.IO.File]::WriteAllText($inputFile, $Text, $utf8NoBom)

    $env:RED_FRAME_TRANSLATOR_PROVIDER = $script:Provider
    if (-not [string]::IsNullOrWhiteSpace($script:MicrosoftKey)) {
        $env:RED_FRAME_TRANSLATOR_MICROSOFT_KEY = $script:MicrosoftKey
    }
    if (-not [string]::IsNullOrWhiteSpace($script:MicrosoftRegion)) {
        $env:RED_FRAME_TRANSLATOR_MICROSOFT_REGION = $script:MicrosoftRegion
    }
    if (-not [string]::IsNullOrWhiteSpace($script:MicrosoftEndpoint)) {
        $env:RED_FRAME_TRANSLATOR_MICROSOFT_ENDPOINT = $script:MicrosoftEndpoint
    }
    if (-not [string]::IsNullOrWhiteSpace($script:DeepLKey)) {
        $env:RED_FRAME_TRANSLATOR_DEEPL_KEY = $script:DeepLKey
    }
    if (-not [string]::IsNullOrWhiteSpace($script:DeepLUrl)) {
        $env:RED_FRAME_TRANSLATOR_DEEPL_URL = $script:DeepLUrl
    }
    if (-not [string]::IsNullOrWhiteSpace($script:MyMemoryEndpoint)) {
        $env:RED_FRAME_TRANSLATOR_MYMEMORY_ENDPOINT = $script:MyMemoryEndpoint
    }

    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Format-ProcessArgument -Value $script:ScriptPath),
        "-WorkerMode",
        "-InputFile", (Format-ProcessArgument -Value $inputFile),
        "-OutputFile", (Format-ProcessArgument -Value $outputFile),
        "-WorkerDirection", (Format-ProcessArgument -Value $Direction)
    ) -join " "

    $process = Start-Process -FilePath $script:PowerShellExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $script:ActiveTranslation = [pscustomobject]@{
        Process = $process
        InputFile = $inputFile
        OutputFile = $outputFile
    }
    $translationTimer.Start()
}

function Finish-TranslationJob {
    param([string]$FallbackError = "翻译进程没有返回结果。")

    if (-not $script:ActiveTranslation) {
        return
    }

    $translationTimer.Stop()
    $job = $script:ActiveTranslation
    $script:ActiveTranslation = $null

    try {
        if (-not (Test-Path -LiteralPath $job.OutputFile)) {
            throw $FallbackError
        }

        $json = [System.IO.File]::ReadAllText($job.OutputFile, [System.Text.Encoding]::UTF8)
        $result = $json | ConvertFrom-Json
        if ($result.ok) {
            $outputBox.Text = [string]$result.text
            $statusLabel.ForeColor = Get-ThemeColor -Name "MutedText"
            $statusLabel.Text = "好了，可以复制。"
        }
        else {
            throw ([string]$result.error)
        }
    }
    catch {
        $statusLabel.ForeColor = Get-ThemeColor -Name "Danger"
        $statusLabel.Text = "没翻成，稍后再试。详情：" + $_.Exception.Message
    }
    finally {
        Set-TranslationBusy -Busy $false
        Remove-TranslationJobFiles -Job $job
    }
}

$translationTimer = New-Object System.Windows.Forms.Timer
$translationTimer.Interval = 250
$translationTimer.Add_Tick({
    if (-not $script:ActiveTranslation) {
        $translationTimer.Stop()
        return
    }

    if (Test-Path -LiteralPath $script:ActiveTranslation.OutputFile) {
        Finish-TranslationJob
        return
    }

    try {
        $script:ActiveTranslation.Process.Refresh()
        if ($script:ActiveTranslation.Process.HasExited) {
            Finish-TranslationJob -FallbackError "翻译中断了，请重试。"
        }
    }
    catch {
        Finish-TranslationJob -FallbackError $_.Exception.Message
    }
})

$translateButton.Add_Click({
    $text = $inputBox.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        $statusLabel.ForeColor = Get-ThemeColor -Name "Warn"
        $statusLabel.Text = "先输入内容。"
        return
    }

    Set-TranslationBusy -Busy $true
    $statusLabel.ForeColor = Get-ThemeColor -Name "MutedText"
    $statusLabel.Text = "翻译中..."

    try {
        Start-TranslationJob -Text $text -Direction ([string]$directionBox.SelectedItem)
    }
    catch {
        $statusLabel.ForeColor = Get-ThemeColor -Name "Danger"
        $statusLabel.Text = "启动失败：" + $_.Exception.Message
        Set-TranslationBusy -Busy $false
    }
})

$copyButton.Add_Click({
    if (-not [string]::IsNullOrWhiteSpace($outputBox.Text)) {
        [System.Windows.Forms.Clipboard]::SetText($outputBox.Text)
        $statusLabel.ForeColor = Get-ThemeColor -Name "MutedText"
        $statusLabel.Text = "已复制到剪贴板。"
    }
})

$clearButton.Add_Click({
    $inputBox.Clear()
    $outputBox.Clear()
    $statusLabel.ForeColor = Get-ThemeColor -Name "MutedText"
    $statusLabel.Text = "准备好了。"
})

$form.Add_Shown({
    $form.Activate()
    $inputBox.Focus()
    $now = Get-BeijingNow
    $timeLabel.Text = $now.ToString("HH:mm:ss")
    $dateLabel.Text = "北京时间 UTC+8  " + $now.ToString("yyyy-MM-dd ddd")
    Apply-Theme
})

[void][System.Windows.Forms.Application]::Run($form)
