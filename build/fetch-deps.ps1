<#
.SYNOPSIS
    ビルドに必要な第三者配布物(BepInEx本体・日本語フォント)を公式配布元から取得する。

.DESCRIPTION
    取得したファイルは third_party\ 以下に置く(リポジトリには含めない)。
    すべてのファイルについて SHA-256 を固定値と照合し、一致しない場合は失敗させる。
    これにより、配布元が改ざんされた場合や通信が壊れた場合に、気付かないまま同梱してしまうことを防ぐ。

    Windows PowerShell 5.1 で動作するように書いている(&& や三項演算子は使わない)。
#>
[CmdletBinding()]
param(
    # 取得先フォルダ。省略時はリポジトリ直下の third_party
    [string]$OutputDir = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 の Invoke-WebRequest は、進捗表示を出していると極端に遅くなる。
# 数MBのフォントを取得するので、このスクリプトの中では進捗表示を止める。
$ProgressPreference = 'SilentlyContinue'

# Windows PowerShell 5.1 では param ブロックの既定値の中で $PSScriptRoot が空になるため、本体側で既定値を決める
if ([string]::IsNullOrEmpty($OutputDir)) {
    $OutputDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'third_party'
}

# 取得対象の一覧。Path は OutputDir からの相対パス。
# ハッシュ値は初回取得時に公式配布元(HTTPS)から得たものを固定している。
$dependencies = @(
    [pscustomobject]@{
        Name   = 'BepInEx 5.4.23.6 (win x64)'
        Url    = 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.6/BepInEx_win_x64_5.4.23.6.zip'
        Path   = 'bepinex\BepInEx_win_x64_5.4.23.6.zip'
        Sha256 = '6c57a714a892efb4e9e1fef63d608a3391f30ec984031c465404e4ee381a0755'
    },
    [pscustomobject]@{
        Name   = 'Noto Sans JP Medium'
        Url    = 'https://raw.githubusercontent.com/notofonts/noto-cjk/Sans2.004/Sans/SubsetOTF/JP/NotoSansJP-Medium.otf'
        Path   = 'fonts\NotoSansJP-Medium.otf'
        Sha256 = 'f396a3b57256e4515be9cb41f7aac54766d654890082a9f1b5c2451b5c093d8a'
    },
    [pscustomobject]@{
        Name   = 'Noto Sans JP Bold'
        Url    = 'https://raw.githubusercontent.com/notofonts/noto-cjk/Sans2.004/Sans/SubsetOTF/JP/NotoSansJP-Bold.otf'
        Path   = 'fonts\NotoSansJP-Bold.otf'
        Sha256 = '1b0edfb500b73a4fa8a4fcaae1bbbd403994e08e73e3e0da37e70d3853f42c5f'
    },
    [pscustomobject]@{
        Name   = 'Noto Sans JP ライセンス (SIL OFL 1.1)'
        Url    = 'https://raw.githubusercontent.com/notofonts/noto-cjk/Sans2.004/LICENSE'
        Path   = 'fonts\OFL.txt'
        Sha256 = '6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2'
    }
)

# ファイルの SHA-256 を小文字16進で返す
function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# 1件分の取得。既に正しいファイルがあれば何もしない。
function Get-Dependency($Dependency, [string]$BaseDir) {
    $target = Join-Path $BaseDir $Dependency.Path
    $targetDir = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    }

    if ((Test-Path -LiteralPath $target) -and ((Get-Sha256 $target) -eq $Dependency.Sha256)) {
        Write-Host "[skip] $($Dependency.Name) (取得済み)"
        return
    }

    Write-Host "[get ] $($Dependency.Name)"
    # 一時ファイルへ落としてから検証し、合格したものだけを所定の場所へ移す
    $temp = "$target.download"
    try {
        # GitHub は TLS 1.2 以上が必須。古い既定値のままの環境でも失敗しないよう明示する
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $Dependency.Url -OutFile $temp -UseBasicParsing
    }
    catch {
        throw "ダウンロードに失敗しました: $($Dependency.Url)`n原因: $($_.Exception.Message)"
    }

    $actual = Get-Sha256 $temp
    if ($actual -ne $Dependency.Sha256) {
        Remove-Item -LiteralPath $temp -Force
        throw "ハッシュが一致しません: $($Dependency.Name)`n期待値: $($Dependency.Sha256)`n実際値: $actual`n配布元の内容が変わった可能性があります。内容を確認したうえで fetch-deps.ps1 の固定値を更新してください。"
    }
    Move-Item -LiteralPath $temp -Destination $target -Force
}

foreach ($dependency in $dependencies) {
    Get-Dependency $dependency $OutputDir
}

# BepInEx は ZIP のままでは参照できないので、検証済みのZIPを展開しておく(展開先は毎回作り直す)
$bepInExZip = Join-Path $OutputDir 'bepinex\BepInEx_win_x64_5.4.23.6.zip'
$bepInExExtracted = Join-Path $OutputDir 'bepinex\extracted'
if (Test-Path -LiteralPath $bepInExExtracted) {
    Remove-Item -LiteralPath $bepInExExtracted -Recurse -Force
}
Expand-Archive -LiteralPath $bepInExZip -DestinationPath $bepInExExtracted
Write-Host "[done] 依存物の取得が完了しました: $OutputDir"
