using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BioIncJp.Patcher.Core.Patching;

/// <summary>適用の依頼内容(不変)。</summary>
/// <param name="GameRoot">ゲームのフォルダ。</param>
/// <param name="PayloadRoot">配布物(ゲームフォルダへ置く中身)のフォルダ。</param>
/// <param name="ToolVersion">パッチツールの版。</param>
/// <param name="GameBuildId">ゲームのビルド番号(不明なら空文字)。</param>
/// <param name="Now">適用日時として記録する時刻。</param>
public sealed record InstallRequest(string GameRoot, string PayloadRoot, string ToolVersion, string GameBuildId, DateTimeOffset Now);

/// <summary>適用の結果(不変)。</summary>
/// <param name="Manifest">保存した導入記録。</param>
/// <param name="CreatedCount">新しく作ったファイルの数。</param>
/// <param name="ReplacedCount">退避してから置き換えたファイルの数。</param>
/// <param name="KeptCount">同じ内容が既にあったので何もしなかったファイルの数。</param>
public sealed record InstallOutcome(InstallManifest Manifest, int CreatedCount, int ReplacedCount, int KeptCount);

/// <summary>
/// パッチの適用を実行する(ファイルを書き込む側)。
/// 何をするかの判断は <see cref="InstallPlanner"/> が行い、ここではその計画どおりに実行するだけ。
/// </summary>
public static class PatchInstaller
{
    /// <summary>
    /// パッチを適用する。途中で失敗した場合は、それまでの変更を取り消してから例外を投げる。
    /// </summary>
    /// <exception cref="PatchException">適用できない状態である、または書き込みに失敗した場合。</exception>
    public static InstallOutcome Install(InstallRequest request, IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = FileSystemProbe.ReadPayload(request.PayloadRoot);
        var snapshot = FileSystemProbe.Capture(
            request.GameRoot,
            InstallPlanner.FilesToProbe(payload),
            InstallPlanner.DirectoriesToProbe(payload));

        var plan = InstallPlanner.CreatePlan(payload, snapshot) switch
        {
            PlanResult.Ready ready => ready.Plan,
            PlanResult.Rejected rejected => throw new PatchException(rejected.Message),
            _ => throw new InvalidOperationException("未対応の計画結果です。"),
        };

        // 先に「適用中」の記録を残す。途中で電源が落ちても、この記録を頼りに元へ戻せる。
        var installing = BuildManifest(request, plan, InstallStatus.Installing);
        try
        {
            ManifestStore.Save(request.GameRoot, installing);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // この時点ではまだ何も配置していない。作りかけの記録用フォルダだけを片付けて、何も変更していない状態に戻す。
            var cleanupNote = RemoveUnusedPatchData(request.GameRoot)
                ? string.Empty
                : $"\n({PatchLayout.PatchDataDirectory} フォルダを削除できませんでした。手動で削除してください)";
            throw new PatchException(
                $"ゲームフォルダへ書き込めないため、適用を中止しました(ゲームのファイルは変更していません)。{cleanupNote}\n原因: {Describe(ex)}", ex);
        }

        try
        {
            Execute(request, plan, progress);
            var installed = installing with { Status = InstallStatus.Installed };
            ManifestStore.Save(request.GameRoot, installed);

            return new InstallOutcome(
                installed,
                plan.Files.Count(file => file.Action == FileAction.Create),
                plan.Files.Count(file => file.Action == FileAction.Replace),
                plan.Files.Count(file => file.Action == FileAction.KeepIdentical));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PatchException)
        {
            progress?.Report("適用に失敗したため、変更を取り消しています…");
            var rollbackError = RollBack(request.GameRoot, installing, progress);

            // 取り消しまで失敗した場合は、その事実を必ず伝える(導入記録は残るので、後から「元に戻す」でやり直せる)
            var outcome = rollbackError is null
                ? "適用に失敗したため、変更を取り消しました。"
                : "適用に失敗し、変更の取り消しも完了できませんでした。原因を取り除いてから、もう一度このツールで「元に戻す」を実行してください。"
                    + $"\n取り消しできなかった理由: {rollbackError}";
            throw new PatchException($"{outcome}\n原因: {Describe(ex)}", ex);
        }
    }

    /// <summary>計画から導入記録を作る(副作用なし)。</summary>
    internal static InstallManifest BuildManifest(InstallRequest request, InstallPlan plan, InstallStatus status)
    {
        var files = plan.Files
            .Select(file => new ManifestFile(
                file.RelativePath,
                file.Action,
                file.Sha256,
                file.Action == FileAction.Replace ? PatchLayout.BackupDirectoryName + "/" + file.RelativePath : null))
            .ToArray();

        return new InstallManifest(
            InstallManifest.CurrentSchemaVersion,
            request.ToolVersion,
            status,
            request.Now,
            request.GameBuildId,
            plan.BepInExPreexisting,
            plan.DirectoriesToCreate,
            files);
    }

    /// <summary>計画どおりにフォルダを作り、ファイルを配置する。</summary>
    private static void Execute(InstallRequest request, InstallPlan plan, IProgress<string>? progress)
    {
        var patchDataRoot = Path.Combine(request.GameRoot, PatchLayout.PatchDataDirectory);

        foreach (var directory in plan.DirectoriesToCreate)
        {
            Directory.CreateDirectory(RelativePaths.ToFullPath(request.GameRoot, directory));
        }

        foreach (var file in plan.Files)
        {
            if (file.Action == FileAction.KeepIdentical)
            {
                continue;
            }

            var source = RelativePaths.ToFullPath(request.PayloadRoot, file.RelativePath);
            var target = RelativePaths.ToFullPath(request.GameRoot, file.RelativePath);

            if (file.Action == FileAction.Replace)
            {
                // 置き換える前に、元のファイルをパッチ用フォルダへ退避する。
                // 元に戻すときは退避ファイルで元の場所を上書きするので、途中までしか書かれていない退避ファイルを残してはいけない。
                var backup = RelativePaths.ToFullPath(patchDataRoot, PatchLayout.BackupDirectoryName + "/" + file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                CopyContentsAtomically(target, backup);
                progress?.Report($"退避: {file.RelativePath}");
            }

            CopyContents(source, target);
            if (!string.Equals(FileHasher.Sha256Hex(target), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new PatchException($"配置したファイルの内容が配布物と一致しません: {file.RelativePath}");
            }

            progress?.Report($"配置: {file.RelativePath}");
        }
    }

    /// <summary>
    /// ファイルの中身だけをコピーする。
    /// File.Copy と違い、Windows が「インターネットから入手したファイル」に付ける目印(代替データストリーム)を引き継がない。
    /// ZIP をダウンロードして展開したファイルにはこの目印が付いており、そのまま配置すると読み込みを妨げられる環境がある。
    /// </summary>
    internal static void CopyContents(string source, string target)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (File.Exists(target))
        {
            // 読み取り専用になっていても上書きできるようにする
            File.SetAttributes(target, FileAttributes.Normal);
        }

        using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
    }

    /// <summary>
    /// ファイルの中身を、書きかけの状態を正式な名前で残さずにコピーする。
    /// まず「正式な名前 + <see cref="TemporaryFileSuffix"/>」へ書き終えてから、正式な名前へ改名する。
    /// 途中で電源が落ちたりプロセスが終了させられたりしても、正式な名前のファイルは「無い」か「完全」のどちらかになる。
    /// </summary>
    internal static void CopyContentsAtomically(string source, string target)
    {
        var temporary = target + TemporaryFileSuffix;
        CopyContents(source, temporary);
        if (File.Exists(target))
        {
            // 以前の中断で残った退避ファイルが読み取り専用でも置き換えられるようにする
            File.SetAttributes(target, FileAttributes.Normal);
        }

        // 同じフォルダ内の改名は、書き込みの途中が外から見えない形で行われる
        File.Move(temporary, target, overwrite: true);
    }

    /// <summary>書きかけのファイルに付ける接尾辞(<see cref="CopyContentsAtomically"/> が使う)。</summary>
    internal const string TemporaryFileSuffix = ".partial";

    /// <summary>
    /// 失敗時の取り消し。成功したら null、取り消し自体が失敗したらその理由を返す。
    /// ここで例外を投げると本来の失敗原因が隠れてしまうため、理由は戻り値で呼び出し元へ渡し、両方をまとめて伝えさせる。
    /// </summary>
    private static string? RollBack(string gameRoot, InstallManifest manifest, IProgress<string>? progress)
    {
        try
        {
            PatchUninstaller.Uninstall(gameRoot, manifest, progress);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PatchException)
        {
            return Describe(ex);
        }
    }

    /// <summary>
    /// 何も配置しないまま終わった場合に、作りかけの記録用フォルダ(書きかけの記録ファイルを含む)を取り除く。
    /// 取り除けた、または元から無い場合は true。
    /// </summary>
    private static bool RemoveUnusedPatchData(string gameRoot)
    {
        var patchDataRoot = Path.Combine(gameRoot, PatchLayout.PatchDataDirectory);
        try
        {
            if (!Directory.Exists(patchDataRoot))
            {
                return true;
            }

            // 消すのは書きかけの記録ファイルだけ。以前の記録や退避ファイルが残っている場合に巻き込まないよう、
            // フォルダは空になった場合に限って削除する。
            var temporaryManifest = ManifestStore.GetManifestPath(gameRoot) + ".tmp";
            if (File.Exists(temporaryManifest))
            {
                File.Delete(temporaryManifest);
            }

            if (!Directory.EnumerateFileSystemEntries(patchDataRoot).Any())
            {
                Directory.Delete(patchDataRoot);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 削除できなかったことは戻り値で伝え、呼び出し元が利用者への説明に含める
            return false;
        }
    }

    /// <summary>例外を利用者向けの短い説明にする。</summary>
    internal static string Describe(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => $"ファイルへの書き込みが拒否されました。ゲームやセキュリティソフトがファイルを使用中でないか確認してください。({exception.Message})",
            _ => exception.Message,
        };
    }
}
