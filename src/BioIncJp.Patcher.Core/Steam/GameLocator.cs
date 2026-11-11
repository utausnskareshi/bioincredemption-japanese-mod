using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace BioIncJp.Patcher.Core.Steam;

/// <summary>
/// 見つかったゲームのインストール情報(不変)。
/// </summary>
/// <param name="RootPath">ゲームのフォルダ(BioIncRedemption.exe がある場所)。</param>
/// <param name="BuildId">Steam が記録しているビルド番号。不明な場合は空文字。</param>
/// <param name="IsFullyInstalled">Steam 上で「インストール完了」の状態か(更新中・検証中なら false)。</param>
public sealed record GameInstallation(string RootPath, string BuildId, bool IsFullyInstalled);

/// <summary>ゲームが見つからなかった理由。</summary>
public enum GameNotFoundReason
{
    /// <summary>Steam 自体が見つからない。</summary>
    SteamNotFound,

    /// <summary>Steam はあるが、このゲームがインストールされていない。</summary>
    GameNotInstalled,

    /// <summary>Steam 上はインストール済みだが、ゲームの実行ファイルが無い(インストール途中など)。</summary>
    GameFilesMissing,
}

/// <summary>ゲーム検出の結果。見つかった場合と見つからなかった場合のどちらかになる。</summary>
public abstract record GameDetectionResult
{
    private GameDetectionResult()
    {
    }

    /// <summary>見つかった。</summary>
    public sealed record Found(GameInstallation Game) : GameDetectionResult;

    /// <summary>見つからなかった。</summary>
    /// <param name="Reason">理由の分類。</param>
    /// <param name="Message">利用者へそのまま見せられる説明。</param>
    public sealed record NotFound(GameNotFoundReason Reason, string Message) : GameDetectionResult;
}

/// <summary>Steam 本体のインストール先の候補を返すもの(テストで差し替えられるよう分離している)。</summary>
public interface ISteamRootProvider
{
    /// <summary>Steam 本体のフォルダの候補(存在確認はしていない)。</summary>
    IReadOnlyList<string> GetSteamRoots();
}

/// <summary>Windows のレジストリから Steam 本体のインストール先を調べる。</summary>
public sealed class RegistrySteamRootProvider : ISteamRootProvider
{
    // Steam は、利用者ごとの設定(HKCU)とシステム全体の設定(HKLM)の両方へインストール先を書く
    private static readonly (RegistryHive Hive, RegistryView View, string Key, string Value)[] Locations =
    {
        (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath"),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Valve\Steam", "InstallPath"),
    };

    /// <inheritdoc />
    public IReadOnlyList<string> GetSteamRoots()
    {
        return Locations
            .Select(location => ReadValue(location.Hive, location.View, location.Key, location.Value))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizePath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>レジストリの文字列値を読む。キーや値が無い場合、読めない場合は null。</summary>
    private static string? ReadValue(RegistryHive hive, RegistryView view, string keyPath, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(keyPath);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // 読めない場所は「候補なし」として扱い、他の候補で検出を続ける
            return null;
        }
    }

    /// <summary>Steam はレジストリへ "c:/program files (x86)/steam" のように書くことがあるので、Windows の表記へ揃える。</summary>
    private static string NormalizePath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
    }
}

/// <summary>
/// Steam のライブラリから Bio Inc. Redemption のインストール先を探す。
/// </summary>
public static class GameLocator
{
    /// <summary>Steam 上のアプリ番号。</summary>
    public const string AppId = "612470";

    /// <summary>ゲームの実行ファイル名。</summary>
    public const string ExecutableName = "BioIncRedemption.exe";

    /// <summary>ゲームの表示名。</summary>
    public const string GameTitle = "Bio Inc. Redemption";

    // Steam がアプリの状態(appmanifest の StateFlags)で「インストール完了」を表すビット
    private const int StateFullyInstalled = 4;

    /// <summary>Steam のインストール先を調べ、ゲームを探す。</summary>
    public static GameDetectionResult Locate(ISteamRootProvider steamRootProvider)
    {
        ArgumentNullException.ThrowIfNull(steamRootProvider);
        return LocateIn(steamRootProvider.GetSteamRoots());
    }

    /// <summary>与えられた Steam 本体フォルダの候補から、ゲームを探す。</summary>
    public static GameDetectionResult LocateIn(IReadOnlyList<string> steamRoots)
    {
        ArgumentNullException.ThrowIfNull(steamRoots);

        var existingRoots = steamRoots.Where(Directory.Exists).ToArray();
        if (existingRoots.Length == 0)
        {
            return new GameDetectionResult.NotFound(
                GameNotFoundReason.SteamNotFound,
                $"Steam が見つかりませんでした。\nこのツールは Steam 版の {GameTitle} 専用です。");
        }

        // Steam 本体のフォルダと、追加したライブラリフォルダのすべてを調べる
        // 読み取りや解析に失敗したファイル。検出は続けるが、最終的に見つからなかった場合は原因として利用者へ伝える。
        var problems = new List<string>();

        var libraries = existingRoots
            .SelectMany(root => new[] { root }.Concat(ReadLibraryPaths(root, problems)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidates = libraries
            .Select(library => TryReadInstallation(library, problems))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToArray();

        var installed = candidates.FirstOrDefault(candidate => candidate.ExecutableExists);
        if (installed is not null)
        {
            return new GameDetectionResult.Found(installed.Game);
        }

        if (candidates.Length > 0)
        {
            return new GameDetectionResult.NotFound(
                GameNotFoundReason.GameFilesMissing,
                $"{GameTitle} のファイルが見つかりませんでした。\nSteam でインストールが完了しているか確認してください。\n({candidates[0].Game.RootPath}){DescribeProblems(problems)}");
        }

        return new GameDetectionResult.NotFound(
            GameNotFoundReason.GameNotInstalled,
            $"{GameTitle} がインストールされていません。\nSteam でゲームをインストールしてから、もう一度実行してください。{DescribeProblems(problems)}");
    }

    /// <summary>
    /// フォルダを直接指定してゲームを確認する(自動検出を使わない場合)。
    /// </summary>
    public static GameDetectionResult FromDirectory(string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(gameDirectory);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(gameDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // フォルダの指定は利用者が入力する値なので、パスとして使えない文字が含まれている場合がある
            return new GameDetectionResult.NotFound(
                GameNotFoundReason.GameFilesMissing,
                $"フォルダの指定が正しくありません: \"{gameDirectory}\"\n({ex.Message})");
        }

        if (!File.Exists(Path.Combine(fullPath, ExecutableName)))
        {
            return new GameDetectionResult.NotFound(
                GameNotFoundReason.GameFilesMissing,
                $"指定されたフォルダに {ExecutableName} がありません。\n({fullPath})");
        }

        return new GameDetectionResult.Found(new GameInstallation(fullPath, BuildId: string.Empty, IsFullyInstalled: true));
    }

    /// <summary>
    /// Steam 本体のフォルダにある libraryfolders.vdf から、ライブラリフォルダの一覧を読む。
    /// ファイルが無い場合は空の一覧を返す(Steam 本体のフォルダだけを調べることになる)。
    /// 読めない・壊れている場合も空の一覧を返すが、その原因を <paramref name="problems"/> へ記録する。
    /// </summary>
    internal static IReadOnlyList<string> ReadLibraryPaths(string steamRoot, ICollection<string> problems)
    {
        // 新しい Steam は steamapps に、古い Steam は config に置く
        var candidates = new[]
        {
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            Path.Combine(steamRoot, "config", "libraryfolders.vdf"),
        };

        return candidates
            .Where(File.Exists)
            .SelectMany(path => ReadLibraryPathsFromFile(path, problems))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>libraryfolders.vdf の内容からライブラリフォルダの一覧を取り出す(解析のみ。副作用なし)。</summary>
    internal static IReadOnlyList<string> ParseLibraryPaths(string vdfText)
    {
        var root = VdfParser.Parse(vdfText);
        var folders = root.Child("libraryfolders");
        if (folders is null)
        {
            return Array.Empty<string>();
        }

        // 新形式: "0" { "path" "D:\\SteamLibrary" ... } / 旧形式: "1" "D:\\SteamLibrary"
        return folders.Children
            .Where(pair => int.TryParse(pair.Key, out _))
            .Select(pair => pair.Value.Value ?? pair.Value.ChildValue("path"))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadLibraryPathsFromFile(string path, ICollection<string> problems)
    {
        try
        {
            return ParseLibraryPaths(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or VdfFormatException)
        {
            // このファイルから読めなくても、他の候補で検出を続けられる。原因は記録しておき、見つからなかった場合に伝える。
            problems.Add($"{path} ({ex.Message})");
            return Array.Empty<string>();
        }
    }

    /// <summary>読み取れなかったファイルがあれば、未検出の説明に添える文章を作る(無ければ空文字)。</summary>
    private static string DescribeProblems(IReadOnlyCollection<string> problems)
    {
        return problems.Count == 0
            ? string.Empty
            : "\n\n次のファイルを読み取れなかったため、検出できなかった可能性があります:\n"
                + string.Join("\n", problems.Select(problem => "・" + problem));
    }

    /// <summary>ライブラリ1つ分の調査結果。</summary>
    private sealed record Candidate(GameInstallation Game, bool ExecutableExists);

    /// <summary>
    /// ライブラリフォルダにこのゲームの管理ファイル(appmanifest)があれば、インストール情報を読む。
    /// 管理ファイルが無い場合は null。読めない・壊れている場合も null を返すが、その原因を <paramref name="problems"/> へ記録する。
    /// </summary>
    private static Candidate? TryReadInstallation(string libraryPath, ICollection<string> problems)
    {
        var manifestPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_{AppId}.acf");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var state = VdfParser.Parse(File.ReadAllText(manifestPath)).Child("AppState");
            var installDirectory = state?.ChildValue("installdir");
            if (state is null || string.IsNullOrWhiteSpace(installDirectory))
            {
                problems.Add($"{manifestPath} (インストール先の情報がありません)");
                return null;
            }

            var gameRoot = Path.Combine(libraryPath, "steamapps", "common", installDirectory);
            var buildId = state.ChildValue("buildid") ?? string.Empty;
            var isFullyInstalled = int.TryParse(state.ChildValue("StateFlags"), out var flags)
                && (flags & StateFullyInstalled) == StateFullyInstalled;

            return new Candidate(
                new GameInstallation(gameRoot, buildId, isFullyInstalled),
                File.Exists(Path.Combine(gameRoot, ExecutableName)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or VdfFormatException)
        {
            problems.Add($"{manifestPath} ({ex.Message})");
            return null;
        }
    }
}
