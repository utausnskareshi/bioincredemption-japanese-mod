<#
.SYNOPSIS
    配布用ZIPを作る(ビルド → 発行 → 同梱物の組み立て → ZIP化)。

.DESCRIPTION
    出力: dist\BioIncRedemptionJP-v<バージョン>.zip と、その SHA-256 を記した .sha256 ファイル。

    ZIP の中身:
      BioIncRedemptionJP.exe   パッチツール(.NET ランタイム同梱の単一EXE)
      payload\                 ゲームフォルダへ置く中身(BepInEx 無改変 + 日本語化プラグイン + 翻訳 + フォント)
      licenses\                同梱している第三者ソフトウェアのライセンス文
      README.md / LICENSE / THIRD-PARTY-NOTICES.md

    プラグインのビルドにはゲーム本体のDLLへの参照が必要なため、ゲームがインストールされているPCで実行する。
    ゲームの場所は Steam から自動検出する(見つからない場合は -GameDir で指定)。

    Windows PowerShell 5.1 で動作するように書いている。

.EXAMPLE
    .\build\build.ps1

.EXAMPLE
    .\build\build.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Bio Inc. Redemption'
#>
[CmdletBinding()]
param(
    # ゲームのインストール先。省略時は local.props、無ければ Steam から自動検出する
    [string]$GameDir = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$steamAppId = '612470'
$gameExe = 'BioIncRedemption.exe'

# ---------------------------------------------------------------- 補助関数

# 外部コマンドを実行し、失敗(終了コードが0以外)なら止める
function Invoke-Checked([string]$Description, [scriptblock]$Command) {
    Write-Host "==> $Description"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description に失敗しました(終了コード $LASTEXITCODE)。"
    }
}

# Directory.Build.props から配布物のバージョンを読む
function Get-ProductVersion([string]$Root) {
    $xml = [xml](Get-Content -LiteralPath (Join-Path $Root 'Directory.Build.props') -Raw -Encoding UTF8)
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/Version')
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw 'Directory.Build.props に Version がありません。'
    }
    return $node.InnerText.Trim()
}

# local.props に書かれたゲームの場所を読む(無ければ空文字)
function Get-GameDirFromLocalProps([string]$Root) {
    $propsPath = Join-Path $Root 'local.props'
    if (-not (Test-Path -LiteralPath $propsPath)) {
        return ''
    }
    $xml = [xml](Get-Content -LiteralPath $propsPath -Raw -Encoding UTF8)
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/GameDir')
    if ($null -eq $node) {
        return ''
    }
    return $node.InnerText
}

# Steam のライブラリを調べて、ゲームのインストール先を探す(見つからなければ空文字)
function Find-GameDirFromSteam {
    $steamRoots = @()
    foreach ($location in @(
            @{ Path = 'HKCU:\Software\Valve\Steam'; Name = 'SteamPath' },
            @{ Path = 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam'; Name = 'InstallPath' },
            @{ Path = 'HKLM:\SOFTWARE\Valve\Steam'; Name = 'InstallPath' })) {
        $item = Get-ItemProperty -Path $location.Path -ErrorAction SilentlyContinue
        if ($null -ne $item -and $null -ne $item.PSObject.Properties[$location.Name]) {
            $steamRoots += ($item.($location.Name) -replace '/', '\')
        }
    }

    # Steam 本体のフォルダに加え、libraryfolders.vdf に書かれた追加ライブラリも調べる
    $libraries = @()
    foreach ($root in ($steamRoots | Select-Object -Unique)) {
        $libraries += $root
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw -Encoding UTF8), '"path"\s+"([^"]+)"')) {
                $libraries += ($match.Groups[1].Value -replace '\\\\', '\')
            }
        }
    }

    foreach ($library in ($libraries | Select-Object -Unique)) {
        $manifest = Join-Path $library "steamapps\appmanifest_$steamAppId.acf"
        if (-not (Test-Path -LiteralPath $manifest)) {
            continue
        }
        $installDir = [regex]::Match((Get-Content -LiteralPath $manifest -Raw -Encoding UTF8), '"installdir"\s+"([^"]+)"').Groups[1].Value
        $candidate = Join-Path $library "steamapps\common\$installDir"
        if (Test-Path -LiteralPath (Join-Path $candidate $gameExe)) {
            return $candidate
        }
    }
    return ''
}

# ---------------------------------------------------------------- 1. ゲームの場所を決める

if ([string]::IsNullOrEmpty($GameDir)) {
    $GameDir = Get-GameDirFromLocalProps $repoRoot
}
if ([string]::IsNullOrEmpty($GameDir) -or -not (Test-Path -LiteralPath (Join-Path $GameDir $gameExe))) {
    $GameDir = Find-GameDirFromSteam
}
if ([string]::IsNullOrEmpty($GameDir)) {
    throw "Bio Inc. Redemption のインストール先が見つかりません。-GameDir で指定してください(プラグインのビルドにゲーム本体のDLLへの参照が必要です)。"
}
Write-Host "ゲームの場所: $GameDir"

# 各プロジェクトが参照できるよう、ゲームの場所を local.props に書き出す(リポジトリには含めない)
$localProps = @"
<Project>
  <!-- このPC固有の設定。build\build.ps1 が自動生成する(リポジトリには含めない) -->
  <PropertyGroup>
    <GameDir>$([System.Security.SecurityElement]::Escape($GameDir))</GameDir>
  </PropertyGroup>
</Project>
"@
[IO.File]::WriteAllText((Join-Path $repoRoot 'local.props'), $localProps, (New-Object Text.UTF8Encoding($false)))

$version = Get-ProductVersion $repoRoot
Write-Host "バージョン: $version"

# ---------------------------------------------------------------- 2. 依存物の取得・ビルド・発行

Invoke-Checked '第三者配布物の取得' { & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'fetch-deps.ps1') }

Invoke-Checked '日本語化プラグインのビルド' {
    & dotnet build (Join-Path $repoRoot 'src\BioIncJp.Plugin\BioIncJp.Plugin.csproj') -c Release -nologo -v q
}

$publishDir = Join-Path $repoRoot 'artifacts\publish'
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
Invoke-Checked 'パッチツールの発行(ランタイム同梱の単一EXE)' {
    & dotnet publish (Join-Path $repoRoot 'src\BioIncJp.Patcher\BioIncJp.Patcher.csproj') -c Release -nologo -v q -o $publishDir
}

# ---------------------------------------------------------------- 3. 配布物の組み立て

$packageName = "BioIncRedemptionJP-v$version"
$distRoot = Join-Path $repoRoot 'dist'
$packageDir = Join-Path $distRoot $packageName
if (Test-Path -LiteralPath $packageDir) {
    Remove-Item -LiteralPath $packageDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

# パッチツール本体
Copy-Item -LiteralPath (Join-Path $publishDir 'BioIncRedemptionJP.exe') -Destination $packageDir

# payload: BepInEx は公式配布物を無改変のまま入れる
$payloadDir = Join-Path $packageDir 'payload'
New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
Copy-Item -Path (Join-Path $repoRoot 'third_party\bepinex\extracted\*') -Destination $payloadDir -Recurse -Force

# payload: 日本語化プラグイン・翻訳・フォント
$pluginDir = Join-Path $payloadDir 'BepInEx\plugins\BioIncRedemptionJP'
$fontDir = Join-Path $pluginDir 'fonts'
New-Item -ItemType Directory -Force -Path $fontDir | Out-Null
$pluginOutput = Join-Path $repoRoot 'src\BioIncJp.Plugin\bin\Release'
Copy-Item -LiteralPath (Join-Path $pluginOutput 'BioIncRedemptionJP.Plugin.dll') -Destination $pluginDir
Copy-Item -LiteralPath (Join-Path $pluginOutput 'BioIncRedemptionJP.Localization.dll') -Destination $pluginDir
Copy-Item -LiteralPath (Join-Path $repoRoot 'translation\ja.xml') -Destination $pluginDir
foreach ($font in @('NotoSansJP-Medium.otf', 'NotoSansJP-Bold.otf', 'OFL.txt')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "third_party\fonts\$font") -Destination $fontDir
}

# ライセンス文(同梱している第三者ソフトウェア + .NET ランタイム)
$licenseDir = Join-Path $packageDir 'licenses'
New-Item -ItemType Directory -Force -Path $licenseDir | Out-Null
Copy-Item -Path (Join-Path $repoRoot 'licenses\*') -Destination $licenseDir -Recurse -Force
$dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
foreach ($pair in @(@('LICENSE.txt', 'dotnet-LICENSE.txt'), @('ThirdPartyNotices.txt', 'dotnet-ThirdPartyNotices.txt'))) {
    $source = Join-Path $dotnetRoot $pair[0]
    if (-not (Test-Path -LiteralPath $source)) {
        throw ".NET のライセンスファイルが見つかりません: $source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $licenseDir $pair[1])
}

# 説明書きと、このツール自身のライセンス
foreach ($document in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
    $source = Join-Path $repoRoot $document
    if (-not (Test-Path -LiteralPath $source)) {
        throw "同梱する文書が見つかりません: $source"
    }
    Copy-Item -LiteralPath $source -Destination $packageDir
}

# ---------------------------------------------------------------- 4. ZIP 化とハッシュの記録

$zipPath = Join-Path $distRoot "$packageName.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# ZIP に記録する各ファイルの時刻を、コミットの日時に揃える。
# ファイルの更新日時をそのまま記録すると、パッチツールの EXE のように作り直すたびに日時が変わるファイルのせいで、
# 同じソースから作っても ZIP のハッシュが毎回変わり、公開した .sha256 と照合して確かめることができなくなるため。
# 環境変数 SOURCE_DATE_EPOCH(再現可能なビルドで広く使われる取り決め)があれば、そちらを優先する。
$entryTime = $null
if (-not [string]::IsNullOrEmpty($env:SOURCE_DATE_EPOCH)) {
    $entryTime = [DateTimeOffset]::FromUnixTimeSeconds([long]$env:SOURCE_DATE_EPOCH)
}
else {
    # git が無い・リポジトリでない場合でもビルドは止めない(時刻を揃えないだけ)。
    # Windows PowerShell 5.1 では、エラーで止める設定のまま外部コマンドの標準エラーを捨てると例外になるため、一時的に緩める。
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $commitTime = & git -C $repoRoot log -1 --format=%ct 2>$null
        if ($LASTEXITCODE -eq 0 -and "$commitTime" -match '^\d+$') {
            $entryTime = [DateTimeOffset]::FromUnixTimeSeconds([long]"$commitTime")
        }
    }
    catch [System.Management.Automation.CommandNotFoundException] {
        # git がインストールされていない
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
}

if ($null -eq $entryTime) {
    Write-Warning 'コミットの日時を取得できないため、ZIP にはファイルの更新日時をそのまま記録します(同じソースから作っても ZIP のハッシュが変わります)。'
}
else {
    # 表示は暦の設定(和暦など)に左右されない書式にする
    Write-Host "ZIP に記録する時刻: $($entryTime.ToString('yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)) (UTC。コミットの日時)"
}

# ZipFile.CreateFromDirectory は、Windows PowerShell 5.1 ではパスの区切りを「\」で格納してしまい、
# Windows 以外の展開ツールで正しく展開できない。ZIP の規格どおり「/」で格納するため、1ファイルずつ追加する。
# 展開したときにフォルダが1つできるよう、先頭にパッケージ名のフォルダを付ける。
$zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in (Get-ChildItem -LiteralPath $packageDir -Recurse -File | Sort-Object FullName)) {
        $relative = $file.FullName.Substring($packageDir.Length).TrimStart('\') -replace '\\', '/'
        # 時刻は中身を書き始める前にしか設定できないため、項目を作って時刻を決めてから中身を書く
        # (ZipFileExtensions.CreateEntryFromFile と同じ処理を、時刻の決め方だけ変えて行う)
        $entry = $zip.CreateEntry("$packageName/$relative", [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = if ($null -ne $entryTime) { $entryTime } else { [DateTimeOffset]$file.LastWriteTime }
        $sourceStream = [IO.File]::OpenRead($file.FullName)
        try {
            $entryStream = $entry.Open()
            try {
                $sourceStream.CopyTo($entryStream)
            }
            finally {
                $entryStream.Dispose()
            }
        }
        finally {
            $sourceStream.Dispose()
        }
    }
}
finally {
    $zip.Dispose()
}

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zipPath.sha256", "$hash  $packageName.zip`n", (New-Object Text.UTF8Encoding($false)))

$sizeMb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
Write-Host ''
Write-Host "[done] $zipPath ($sizeMb MB)"
Write-Host "       SHA-256: $hash"
