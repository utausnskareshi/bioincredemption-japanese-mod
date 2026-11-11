using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BioIncJp.Patcher.Core.Patching;
using BioIncJp.Patcher.Core.Steam;

namespace BioIncJp.Patcher.Core;

/// <summary>ゲームフォルダに対するパッチの状態。</summary>
public enum PatchState
{
    /// <summary>日本語化されていない。</summary>
    NotPatched,

    /// <summary>このツールで日本語化済み(導入記録がある)。</summary>
    Patched,

    /// <summary>
    /// 日本語化プラグインはあるが、導入記録が無い・読めない(手動で導入した、記録が失われた等)。
    /// 「元に戻す」は、配布物と照らし合わせて取り除く対象を判断する。
    /// </summary>
    PatchedWithoutRecord,
}

/// <summary>パッチの状態の調査結果(不変)。</summary>
/// <param name="State">状態。</param>
/// <param name="Manifest">導入記録(ある場合)。</param>
/// <param name="Note">利用者へ補足として見せる説明(記録が壊れている等)。無ければ null。</param>
public sealed record PatchStatus(PatchState State, InstallManifest? Manifest, string? Note)
{
    /// <summary>日本語化されているか(記録の有無を問わない)。</summary>
    public bool IsPatched => State != PatchState.NotPatched;
}

/// <summary>適用・復元の操作結果(不変)。</summary>
/// <param name="Succeeded">成功したか。</param>
/// <param name="Message">利用者へそのまま見せられる説明。</param>
public sealed record OperationResult(bool Succeeded, string Message);

/// <summary>
/// パッチツールの画面や自動検証から呼び出す窓口。
/// 「状態を調べる」「日本語化する」「元に戻す」の3つだけを提供し、想定される失敗は例外ではなく結果として返す。
/// </summary>
public sealed class PatchService
{
    private readonly string _payloadRoot;
    private readonly string _toolVersion;
    private readonly Func<bool> _isGameRunning;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// 窓口を作る。
    /// </summary>
    /// <param name="payloadRoot">配布物(ゲームフォルダへ置く中身)のフォルダ。</param>
    /// <param name="toolVersion">パッチツールの版。</param>
    /// <param name="isGameRunning">ゲームが起動中かを返す関数。省略時は実際のプロセスを調べる(テストで差し替えるための引数)。</param>
    /// <param name="clock">現在時刻を返す関数。省略時は実際の時刻(テストで差し替えるための引数)。</param>
    public PatchService(string payloadRoot, string toolVersion, Func<bool>? isGameRunning = null, Func<DateTimeOffset>? clock = null)
    {
        _payloadRoot = payloadRoot ?? throw new ArgumentNullException(nameof(payloadRoot));
        _toolVersion = toolVersion ?? throw new ArgumentNullException(nameof(toolVersion));
        _isGameRunning = isGameRunning ?? IsGameProcessRunning;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>ゲームフォルダのパッチの状態を調べる(読み取りのみ)。</summary>
    public static PatchStatus GetStatus(GameInstallation game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var pluginExists = File.Exists(RelativePaths.ToFullPath(game.RootPath, PatchLayout.PluginAssembly));
        try
        {
            var manifest = ManifestStore.TryLoad(game.RootPath);
            if (manifest is not null)
            {
                var note = manifest.Status == InstallStatus.Installing
                    ? "前回の日本語化が途中で中断されています。「元に戻す」を実行してから、もう一度日本語化してください。"
                    : null;
                return new PatchStatus(PatchState.Patched, manifest, note);
            }

            return pluginExists
                ? new PatchStatus(
                    PatchState.PatchedWithoutRecord,
                    null,
                    "このツールの導入記録がありません(手動で導入された可能性があります)。「元に戻す」では、配布物と同じファイルを取り除きます。")
                : new PatchStatus(PatchState.NotPatched, null, null);
        }
        catch (PatchException ex)
        {
            // 記録が壊れていても「元に戻す」は実行できるようにする
            return new PatchStatus(PatchState.PatchedWithoutRecord, null, ex.Message);
        }
    }

    /// <summary>日本語化する。</summary>
    public OperationResult Apply(GameInstallation game, IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (_isGameRunning())
        {
            return new OperationResult(false, $"{GameLocator.GameTitle} が起動中です。ゲームを終了してから、もう一度実行してください。");
        }

        if (GetStatus(game).IsPatched)
        {
            return new OperationResult(false, "既に日本語化されています。");
        }

        try
        {
            var outcome = PatchInstaller.Install(
                new InstallRequest(game.RootPath, _payloadRoot, _toolVersion, game.BuildId, _clock()),
                progress);

            var loaderNote = outcome.Manifest.BepInExPreexisting
                ? "\n(BepInEx が導入済みだったため、日本語化プラグインだけを追加しました。)"
                : string.Empty;
            return new OperationResult(true, $"日本語化が完了しました。ゲームを起動すると日本語で表示されます。{loaderNote}");
        }
        catch (Exception ex) when (ex is PatchException or IOException or UnauthorizedAccessException)
        {
            return new OperationResult(false, PatchInstaller.Describe(ex));
        }
    }

    /// <summary>元に戻す(日本語化を取り除く)。</summary>
    public OperationResult Revert(GameInstallation game, IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (_isGameRunning())
        {
            return new OperationResult(false, $"{GameLocator.GameTitle} が起動中です。ゲームを終了してから、もう一度実行してください。");
        }

        var status = GetStatus(game);
        if (!status.IsPatched)
        {
            return new OperationResult(false, "日本語化されていないため、元に戻すものがありません。");
        }

        try
        {
            // 導入記録が無い場合は、配布物と照らし合わせて代わりの記録を作る
            var manifest = status.Manifest
                ?? PatchUninstaller.SynthesizeManifest(game.RootPath, FileSystemProbe.ReadPayload(_payloadRoot), _toolVersion, _clock());

            var outcome = PatchUninstaller.Uninstall(game.RootPath, manifest, progress);
            return new OperationResult(true, DescribeRevert(outcome));
        }
        catch (Exception ex) when (ex is PatchException or IOException or UnauthorizedAccessException)
        {
            return new OperationResult(false, PatchInstaller.Describe(ex));
        }
    }

    /// <summary>復元結果を利用者向けの説明にする(副作用なし)。</summary>
    internal static string DescribeRevert(UninstallOutcome outcome)
    {
        var lines = new List<string> { "元に戻しました。" };

        if (outcome.LoaderKept && outcome.LoaderWasPreexisting)
        {
            lines.Add("BepInEx は日本語化の前から導入されていたため、そのまま残しています。");
        }
        else if (outcome.LoaderKept)
        {
            lines.Add($"他のMODのファイルが見つかったため、BepInEx 本体は残しています({outcome.ForeignFiles.Count} 件。例: {outcome.ForeignFiles.FirstOrDefault()})。");
        }

        if (outcome.Leftovers.Count > 0)
        {
            lines.Add($"想定外のファイルが残っているフォルダがあります({outcome.Leftovers.Count} 件。例: {outcome.Leftovers[0]})。");
        }

        return string.Join("\n", lines);
    }

    /// <summary>ゲームのプロセスが動いているか。</summary>
    private static bool IsGameProcessRunning()
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameLocator.ExecutableName));
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
