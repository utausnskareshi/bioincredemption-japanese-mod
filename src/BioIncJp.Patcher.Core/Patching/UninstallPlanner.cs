using System;
using System.Collections.Generic;
using System.Linq;

namespace BioIncJp.Patcher.Core.Patching;

/// <summary>退避しておいたファイルを元の場所へ戻す操作1つ分。</summary>
/// <param name="BackupPath">退避先(パッチ用フォルダからの相対パス)。</param>
/// <param name="TargetPath">戻し先(ゲームフォルダからの相対パス)。</param>
public sealed record RestoreOperation(string BackupPath, string TargetPath);

/// <summary>「元に戻す」の計画(不変)。</summary>
/// <param name="FilesToDelete">削除するファイル(ゲームフォルダからの相対パス)。</param>
/// <param name="FilesToRestore">退避先から戻すファイル。</param>
/// <param name="DirectoriesToPrune">中身が空になっていれば取り除くフォルダ(その配下の空フォルダも含めて取り除く)。</param>
/// <param name="LoaderKept">MODローダー(BepInEx)を残すか。他のMODが使っている場合や、適用前からあった場合は残す。</param>
/// <param name="ForeignFiles">このパッチのものではないファイル(他のMODなど)。ローダーを残す理由になったもの。</param>
public sealed record UninstallPlan(
    IReadOnlyList<string> FilesToDelete,
    IReadOnlyList<RestoreOperation> FilesToRestore,
    IReadOnlyList<string> DirectoriesToPrune,
    bool LoaderKept,
    IReadOnlyList<string> ForeignFiles);

/// <summary>
/// 「元に戻す」の計画を立てる。入力(導入記録と、BepInEx フォルダ内の現在のファイル一覧)だけから結果が決まる純粋な関数。
/// </summary>
public static class UninstallPlanner
{
    /// <summary>
    /// 計画を立てる。
    /// </summary>
    /// <param name="manifest">導入記録。</param>
    /// <param name="existingLoaderFiles">ゲームフォルダの BepInEx フォルダ以下に現在あるファイル(ゲームフォルダからの相対パス)。</param>
    public static UninstallPlan CreatePlan(InstallManifest manifest, IReadOnlyList<string> existingLoaderFiles)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(existingLoaderFiles);

        var owned = manifest.Files.Select(file => RelativePaths.Normalize(file.Path)).ToHashSet(RelativePaths.Comparer);
        var existing = existingLoaderFiles.Select(RelativePaths.Normalize).ToArray();

        // このパッチが置いたものでも、BepInEx が自動で作ったものでもないファイル = 他のMODなど
        var foreign = existing
            .Where(path => !owned.Contains(path) && !PatchLayout.IsGeneratedByLoader(path))
            .ToArray();

        // BepInEx が適用前からあった場合と、他のMODが入っている場合は、BepInEx 本体を残して日本語化プラグインだけを取り除く
        var keepLoader = manifest.BepInExPreexisting || foreign.Length > 0;
        var filesInScope = manifest.Files
            .Where(file => !keepLoader || PatchLayout.IsPluginFile(file.Path))
            .ToArray();

        var filesToDelete = filesInScope
            .Where(file => file.Action is FileAction.Create or FileAction.KeepIdentical)
            .Select(file => RelativePaths.Normalize(file.Path))
            // プラグインの設定ファイルは常に取り除く。BepInEx ごと取り除く場合は、BepInEx が自動で作ったファイルも取り除く。
            .Concat(existing.Where(path => keepLoader
                ? RelativePaths.Comparer.Equals(path, PatchLayout.PluginConfigFile)
                : PatchLayout.IsGeneratedByLoader(path)))
            .Distinct(RelativePaths.Comparer)
            .ToArray();

        var filesToRestore = filesInScope
            .Where(file => file.Action == FileAction.Replace && !string.IsNullOrEmpty(file.BackupPath))
            .Select(file => new RestoreOperation(RelativePaths.Normalize(file.BackupPath!), RelativePaths.Normalize(file.Path)))
            .ToArray();

        var directoriesToPrune = TopMost(
            manifest.CreatedDirectories
                .Select(RelativePaths.Normalize)
                .Where(directory => !keepLoader || IsPluginDirectory(directory)));

        return new UninstallPlan(filesToDelete, filesToRestore, directoriesToPrune, keepLoader, foreign);
    }

    /// <summary>日本語化プラグインのフォルダ自身か、その配下か。</summary>
    private static bool IsPluginDirectory(string directory)
    {
        return RelativePaths.Comparer.Equals(directory, PatchLayout.PluginDirectory)
            || RelativePaths.IsUnder(directory, PatchLayout.PluginDirectory);
    }

    /// <summary>
    /// 入れ子になったフォルダの一覧から、他のフォルダに含まれない最上位のものだけを残す。
    /// (最上位のフォルダを「配下の空フォルダごと取り除く」ので、配下を個別に挙げる必要が無い)
    /// </summary>
    private static IReadOnlyList<string> TopMost(IEnumerable<string> directories)
    {
        var all = directories.Distinct(RelativePaths.Comparer).ToArray();
        return all
            .Where(directory => !all.Any(other => RelativePaths.IsUnder(directory, other)))
            .OrderBy(directory => directory, RelativePaths.Comparer)
            .ToArray();
    }
}
