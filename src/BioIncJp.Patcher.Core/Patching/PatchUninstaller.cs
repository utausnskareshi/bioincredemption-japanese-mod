using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BioIncJp.Patcher.Core.Patching;

/// <summary>「元に戻す」の結果(不変)。</summary>
/// <param name="DeletedCount">削除したファイルの数。</param>
/// <param name="RestoredCount">退避先から戻したファイルの数。</param>
/// <param name="LoaderKept">MODローダー(BepInEx)を残したか。</param>
/// <param name="LoaderWasPreexisting">ローダーが適用前から導入されていたか。</param>
/// <param name="ForeignFiles">ローダーを残す理由になった、他のMODなどのファイル。</param>
/// <param name="Leftovers">取り除く対象のフォルダに残ったファイル(想定外のもの)。</param>
public sealed record UninstallOutcome(
    int DeletedCount,
    int RestoredCount,
    bool LoaderKept,
    bool LoaderWasPreexisting,
    IReadOnlyList<string> ForeignFiles,
    IReadOnlyList<string> Leftovers);

/// <summary>
/// 「元に戻す」を実行する(ファイルを削除・復元する側)。
/// 何をするかの判断は <see cref="UninstallPlanner"/> が行い、ここではその計画どおりに実行するだけ。
/// 途中で失敗しても導入記録は最後まで残すので、やり直せば続きから実行できる。
/// </summary>
public static class PatchUninstaller
{
    /// <summary>
    /// 導入記録に基づいて、パッチを取り除く。
    /// </summary>
    /// <exception cref="PatchException">ファイルを削除・復元できなかった場合。</exception>
    public static UninstallOutcome Uninstall(string gameRoot, InstallManifest manifest, IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(gameRoot);
        ArgumentNullException.ThrowIfNull(manifest);

        var plan = UninstallPlanner.CreatePlan(manifest, FileSystemProbe.ListFiles(gameRoot, PatchLayout.BepInExDirectory));
        var patchDataRoot = Path.Combine(gameRoot, PatchLayout.PatchDataDirectory);

        try
        {
            var restored = plan.FilesToRestore.Count(operation => Restore(gameRoot, patchDataRoot, operation, progress));
            var deleted = plan.FilesToDelete.Count(path => Delete(gameRoot, path, progress));

            foreach (var directory in plan.DirectoriesToPrune)
            {
                PruneEmptyDirectories(RelativePaths.ToFullPath(gameRoot, directory));
            }

            var leftovers = plan.DirectoriesToPrune
                .SelectMany(directory => FileSystemProbe.ListFiles(gameRoot, directory))
                .ToArray();

            // 導入記録と退避ファイルは、すべて片付いた最後に削除する
            if (Directory.Exists(patchDataRoot))
            {
                ClearReadOnly(patchDataRoot);
                Directory.Delete(patchDataRoot, recursive: true);
            }

            return new UninstallOutcome(deleted, restored, plan.LoaderKept, manifest.BepInExPreexisting, plan.ForeignFiles, leftovers);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PatchException(
                $"ファイルを取り除けませんでした。ゲームやセキュリティソフトがファイルを使用中でないか確認して、もう一度実行してください。\n原因: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// 導入記録が無い状態(手動で導入した、記録が失われた等)から取り除くための、代わりの記録を作る。
    /// 配布物と照らし合わせ、「日本語化プラグインのフォルダにあるファイル」と
    /// 「配布物とまったく同じ内容の BepInEx のファイル」を、このパッチのものとみなす。
    /// </summary>
    public static InstallManifest SynthesizeManifest(string gameRoot, IReadOnlyList<PayloadFile> payload, string toolVersion, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(gameRoot);
        ArgumentNullException.ThrowIfNull(payload);

        var snapshot = FileSystemProbe.Capture(
            gameRoot,
            InstallPlanner.FilesToProbe(payload),
            Array.Empty<string>());

        bool IsSameAsPayload(PayloadFile file) =>
            snapshot.FileHashes.TryGetValue(RelativePaths.Normalize(file.RelativePath), out var hash)
            && string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase);

        // BepInEx 本体が配布物と同じ内容なら、このパッチが置いたものと判断する。違うなら、利用者が別途入れたものなので触らない。
        var marker = payload.FirstOrDefault(file => RelativePaths.Comparer.Equals(RelativePaths.Normalize(file.RelativePath), PatchLayout.BepInEx5Marker));
        var loaderIsOurs = marker is not null && IsSameAsPayload(marker);

        var files = payload
            .Where(file => PatchLayout.IsPluginFile(file.RelativePath)
                ? snapshot.FileHashes.ContainsKey(RelativePaths.Normalize(file.RelativePath))
                : loaderIsOurs && IsSameAsPayload(file))
            .Select(file => new ManifestFile(RelativePaths.Normalize(file.RelativePath), FileAction.Create, file.Sha256, BackupPath: null))
            .ToArray();

        var directories = files
            .SelectMany(file => RelativePaths.Ancestors(file.Path))
            .Distinct(RelativePaths.Comparer)
            .OrderBy(RelativePaths.Depth)
            .ToArray();

        return new InstallManifest(
            InstallManifest.CurrentSchemaVersion,
            toolVersion,
            InstallStatus.Installed,
            now,
            GameBuildId: string.Empty,
            BepInExPreexisting: !loaderIsOurs,
            directories,
            files);
    }

    /// <summary>退避しておいたファイルを元の場所へ戻す。退避ファイルが無ければ何もせず false を返す。</summary>
    private static bool Restore(string gameRoot, string patchDataRoot, RestoreOperation operation, IProgress<string>? progress)
    {
        var backup = RelativePaths.ToFullPath(patchDataRoot, operation.BackupPath);
        if (!File.Exists(backup))
        {
            // 適用の途中で失敗し、退避まで進んでいなかった場合。元のファイルはそのまま残っているので、何もしなくてよい。
            return false;
        }

        var target = RelativePaths.ToFullPath(gameRoot, operation.TargetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        PatchInstaller.CopyContents(backup, target);
        progress?.Report($"復元: {operation.TargetPath}");
        return true;
    }

    /// <summary>ファイルを削除する。既に無ければ何もせず false を返す。</summary>
    private static bool Delete(string gameRoot, string relativePath, IProgress<string>? progress)
    {
        var path = RelativePaths.ToFullPath(gameRoot, relativePath);
        if (!File.Exists(path))
        {
            return false;
        }

        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
        progress?.Report($"削除: {relativePath}");
        return true;
    }

    /// <summary>
    /// フォルダの配下から空のフォルダを取り除き、最後にそのフォルダ自身も空なら取り除く。
    /// ファイルが残っているフォルダには触れない。
    /// </summary>
    private static void PruneEmptyDirectories(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.GetDirectories(directory))
        {
            PruneEmptyDirectories(child);
        }

        if (!Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    /// <summary>フォルダ以下のファイルの読み取り専用属性を外す(削除できるようにするため)。</summary>
    private static void ClearReadOnly(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }
}
