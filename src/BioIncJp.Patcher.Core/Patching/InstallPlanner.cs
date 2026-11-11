using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BioIncJp.Patcher.Core.Patching;

/// <summary>配布物に含まれるファイル1つ分の情報。</summary>
/// <param name="RelativePath">配置先のゲームフォルダからの相対パス('/' 区切り)。</param>
/// <param name="Size">バイト数。</param>
/// <param name="Sha256">SHA-256(小文字16進)。</param>
public sealed record PayloadFile(string RelativePath, long Size, string Sha256);

/// <summary>
/// 適用前のゲームフォルダの状態のうち、計画に必要な部分だけを写し取ったもの。
/// 計画の作成(<see cref="InstallPlanner"/>)をファイルシステムから切り離し、純粋な関数として検証できるようにする。
/// </summary>
/// <param name="FileHashes">調べたファイルのうち、存在したものの相対パス → SHA-256。</param>
/// <param name="ExistingDirectories">調べたフォルダのうち、存在したものの相対パス。</param>
public sealed record TargetSnapshot(
    IReadOnlyDictionary<string, string> FileHashes,
    IReadOnlySet<string> ExistingDirectories);

/// <summary>計画に含まれるファイル1つ分。</summary>
/// <param name="RelativePath">ゲームフォルダからの相対パス。</param>
/// <param name="Action">行う操作。</param>
/// <param name="Sha256">配置するファイルの SHA-256。</param>
public sealed record PlannedFile(string RelativePath, FileAction Action, string Sha256);

/// <summary>適用の計画(不変)。</summary>
/// <param name="Files">配置するファイル。</param>
/// <param name="DirectoriesToCreate">新しく作るフォルダ(浅い順)。</param>
/// <param name="BepInExPreexisting">既に BepInEx が導入されているため、BepInEx 本体は配置しない。</param>
public sealed record InstallPlan(
    IReadOnlyList<PlannedFile> Files,
    IReadOnlyList<string> DirectoriesToCreate,
    bool BepInExPreexisting);

/// <summary>計画を立てられなかった理由。</summary>
public enum PlanRejection
{
    /// <summary>このパッチと互換性の無い BepInEx 6 が導入されている。</summary>
    IncompatibleBepInEx,

    /// <summary>BepInEx 以外のMODローダーが、同じ入口のDLL(winhttp.dll)を使っている。</summary>
    ConflictingLoader,

    /// <summary>配布物が空、または必要なファイルが欠けている。</summary>
    InvalidPayload,
}

/// <summary>計画作成の結果。計画が立った場合と、立てられなかった場合のどちらかになる。</summary>
public abstract record PlanResult
{
    private PlanResult()
    {
    }

    /// <summary>計画が立った。</summary>
    public sealed record Ready(InstallPlan Plan) : PlanResult;

    /// <summary>計画を立てられなかった。</summary>
    /// <param name="Reason">理由の分類。</param>
    /// <param name="Message">利用者へそのまま見せられる説明。</param>
    public sealed record Rejected(PlanRejection Reason, string Message) : PlanResult;
}

/// <summary>
/// 適用の計画を立てる。入力(配布物の一覧と、ゲームフォルダの状態の写し)だけから結果が決まる純粋な関数。
/// </summary>
public static class InstallPlanner
{
    /// <summary>
    /// 計画に必要な、ゲームフォルダ側の調査対象のファイル一覧を返す。
    /// 呼び出し側は、これらのファイルの有無とハッシュを調べて <see cref="TargetSnapshot"/> を作る。
    /// </summary>
    public static IReadOnlyList<string> FilesToProbe(IReadOnlyList<PayloadFile> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return payload
            .Select(file => RelativePaths.Normalize(file.RelativePath))
            .Concat(new[] { PatchLayout.BepInEx5Marker, PatchLayout.BepInEx6Marker })
            .Distinct(RelativePaths.Comparer)
            .ToArray();
    }

    /// <summary>計画に必要な、ゲームフォルダ側の調査対象のフォルダ一覧を返す(配布物の各ファイルの親フォルダすべて)。</summary>
    public static IReadOnlyList<string> DirectoriesToProbe(IReadOnlyList<PayloadFile> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return payload
            .SelectMany(file => RelativePaths.Ancestors(file.RelativePath))
            .Distinct(RelativePaths.Comparer)
            .ToArray();
    }

    /// <summary>
    /// 適用の計画を立てる。
    /// </summary>
    /// <param name="payload">配布物のファイル一覧。</param>
    /// <param name="target">ゲームフォルダの状態の写し。</param>
    public static PlanResult CreatePlan(IReadOnlyList<PayloadFile> payload, TargetSnapshot target)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(target);

        var hasPlugin = payload.Any(file => RelativePaths.Comparer.Equals(RelativePaths.Normalize(file.RelativePath), PatchLayout.PluginAssembly));
        if (!hasPlugin)
        {
            return new PlanResult.Rejected(
                PlanRejection.InvalidPayload,
                "配布物の中に日本語化プラグインが見つかりません。ZIP をすべて展開してから実行してください(payload フォルダが EXE と同じ場所に必要です)。");
        }

        if (target.FileHashes.ContainsKey(PatchLayout.BepInEx6Marker))
        {
            return new PlanResult.Rejected(
                PlanRejection.IncompatibleBepInEx,
                "このゲームには BepInEx 6 が導入されています。日本語化パッチは BepInEx 5 用のため、適用できません。");
        }

        // 既に BepInEx 5 があるなら、それをそのまま使う。BepInEx 本体は置き換えず、日本語化プラグインだけを追加する。
        var bepInExPreexisting = target.FileHashes.ContainsKey(PatchLayout.BepInEx5Marker);
        var filesToInstall = payload
            .Where(file => !bepInExPreexisting || PatchLayout.IsPluginFile(file.RelativePath))
            .ToArray();

        // 使う予定のファイルが配布物に揃っているかを、ゲームフォルダに触れる前に確かめる。
        // 欠けたまま導入すると、日本語にならないのに「完了しました」と表示してしまうため。
        var missing = MissingRequiredFiles(payload, bepInExPreexisting);
        if (missing.Count > 0)
        {
            return new PlanResult.Rejected(
                PlanRejection.InvalidPayload,
                "配布物の一部のファイルが見つからないため、適用を中止しました(ゲームのファイルは変更していません)。\n" +
                "ウイルス対策ソフトに隔離されたか、ZIP の展開に失敗した可能性があります。ZIP をもう一度すべて展開してから実行してください。\n" +
                $"見つからないファイル: {string.Join(", ", missing)}");
        }

        if (!bepInExPreexisting && HasConflictingProxy(payload, target))
        {
            return new PlanResult.Rejected(
                PlanRejection.ConflictingLoader,
                $"ゲームフォルダに、別のMODが使っている {PatchLayout.ProxyLibrary} があります。上書きすると そのMODが動かなくなるため、適用を中止しました。");
        }

        var files = filesToInstall
            .Select(file => new PlannedFile(RelativePaths.Normalize(file.RelativePath), DecideAction(file, target), file.Sha256))
            .ToArray();

        var directories = files
            .SelectMany(file => RelativePaths.Ancestors(file.RelativePath))
            .Distinct(RelativePaths.Comparer)
            .Where(directory => !target.ExistingDirectories.Contains(directory))
            .OrderBy(RelativePaths.Depth)
            .ThenBy(directory => directory, RelativePaths.Comparer)
            .ToArray();

        return new PlanResult.Ready(new InstallPlan(files, directories, bepInExPreexisting));
    }

    /// <summary>
    /// 配布物に無い必須ファイルの一覧(副作用なし)。
    /// BepInEx が導入済みなら BepInEx 本体は使わないので、日本語化プラグインのファイルだけを確かめる。
    /// </summary>
    private static IReadOnlyList<string> MissingRequiredFiles(IReadOnlyList<PayloadFile> payload, bool bepInExPreexisting)
    {
        var available = payload.Select(file => RelativePaths.Normalize(file.RelativePath)).ToHashSet(RelativePaths.Comparer);
        var required = bepInExPreexisting
            ? PatchLayout.RequiredPluginFiles
            : PatchLayout.RequiredLoaderFiles.Concat(PatchLayout.RequiredPluginFiles);
        return required.Where(path => !available.Contains(path)).ToArray();
    }

    /// <summary>ファイル1つに対する操作を決める。</summary>
    private static FileAction DecideAction(PayloadFile file, TargetSnapshot target)
    {
        if (!target.FileHashes.TryGetValue(RelativePaths.Normalize(file.RelativePath), out var existingHash))
        {
            return FileAction.Create;
        }

        return string.Equals(existingHash, file.Sha256, StringComparison.OrdinalIgnoreCase)
            ? FileAction.KeepIdentical
            : FileAction.Replace;
    }

    /// <summary>
    /// BepInEx が無いのに、内容の違う winhttp.dll が既にあるか。
    /// その場合は別のMODローダーの入口である可能性が高く、上書きしてはいけない。
    /// </summary>
    private static bool HasConflictingProxy(IReadOnlyList<PayloadFile> payload, TargetSnapshot target)
    {
        var proxy = payload.FirstOrDefault(file => RelativePaths.Comparer.Equals(RelativePaths.Normalize(file.RelativePath), PatchLayout.ProxyLibrary));
        return proxy is not null
            && target.FileHashes.TryGetValue(PatchLayout.ProxyLibrary, out var existingHash)
            && !string.Equals(existingHash, proxy.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>配布物フォルダとゲームフォルダの状態を調べる(ファイルシステムを読むだけで、書き込みはしない)。</summary>
public static class FileSystemProbe
{
    /// <summary>配布物フォルダ内の全ファイルを列挙する。</summary>
    /// <exception cref="PatchException">配布物フォルダが無い場合。</exception>
    public static IReadOnlyList<PayloadFile> ReadPayload(string payloadRoot)
    {
        ArgumentNullException.ThrowIfNull(payloadRoot);

        if (!Directory.Exists(payloadRoot))
        {
            throw new PatchException(
                $"配布物のフォルダが見つかりません: {payloadRoot}\nZIP をすべて展開してから実行してください(payload フォルダが EXE と同じ場所に必要です)。");
        }

        return Directory
            .EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories)
            .Select(path => new PayloadFile(
                RelativePaths.Normalize(Path.GetRelativePath(payloadRoot, path)),
                new FileInfo(path).Length,
                FileHasher.Sha256Hex(path)))
            .OrderBy(file => file.RelativePath, RelativePaths.Comparer)
            .ToArray();
    }

    /// <summary>ゲームフォルダについて、指定されたファイルとフォルダの状態を写し取る。</summary>
    public static TargetSnapshot Capture(string gameRoot, IReadOnlyList<string> files, IReadOnlyList<string> directories)
    {
        ArgumentNullException.ThrowIfNull(gameRoot);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(directories);

        var hashes = files
            .Select(relative => (Relative: RelativePaths.Normalize(relative), Full: RelativePaths.ToFullPath(gameRoot, relative)))
            .Where(item => File.Exists(item.Full))
            .ToDictionary(item => item.Relative, item => FileHasher.Sha256Hex(item.Full), RelativePaths.Comparer);

        var existingDirectories = directories
            .Select(RelativePaths.Normalize)
            .Where(relative => Directory.Exists(RelativePaths.ToFullPath(gameRoot, relative)))
            .ToHashSet(RelativePaths.Comparer);

        return new TargetSnapshot(hashes, existingDirectories);
    }

    /// <summary>ゲームフォルダの指定フォルダ以下にある全ファイルを、ゲームフォルダからの相対パスで列挙する。</summary>
    public static IReadOnlyList<string> ListFiles(string gameRoot, string relativeDirectory)
    {
        var full = RelativePaths.ToFullPath(gameRoot, relativeDirectory);
        if (!Directory.Exists(full))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(full, "*", SearchOption.AllDirectories)
            .Select(path => RelativePaths.Normalize(Path.GetRelativePath(gameRoot, path)))
            .OrderBy(path => path, RelativePaths.Comparer)
            .ToArray();
    }
}
