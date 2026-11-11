using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace BioIncJp.Patcher.Core.Patching;

/// <summary>
/// 利用者へそのまま見せられる説明を持つ、パッチ処理の失敗。
/// 「ゲームが起動中」「別のMODと衝突する」のように、原因と対処が分かっている失敗に使う。
/// </summary>
public sealed class PatchException : Exception
{
    public PatchException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// ゲームフォルダ内で、このパッチが使う場所の取り決め。
/// パスはすべてゲームフォルダからの相対パスで、区切りは '/' に統一する。
/// </summary>
public static class PatchLayout
{
    /// <summary>導入記録と、上書き前のファイルの退避先を置くフォルダ。「元に戻す」で丸ごと削除される。</summary>
    public const string PatchDataDirectory = "BioIncRedemptionJP_PatchData";

    /// <summary>導入記録のファイル名(<see cref="PatchDataDirectory"/> の直下)。</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>上書き前のファイルを退避するフォルダ名(<see cref="PatchDataDirectory"/> の直下)。</summary>
    public const string BackupDirectoryName = "backup";

    /// <summary>MODローダー BepInEx のフォルダ。</summary>
    public const string BepInExDirectory = "BepInEx";

    /// <summary>日本語化プラグインを置くフォルダ。このフォルダはこのパッチ専用。</summary>
    public const string PluginDirectory = "BepInEx/plugins/BioIncRedemptionJP";

    /// <summary>日本語化プラグイン本体。これがあれば「日本語化が導入されている」と判断する。</summary>
    public const string PluginAssembly = "BepInEx/plugins/BioIncRedemptionJP/BioIncRedemptionJP.Plugin.dll";

    /// <summary>日本語化プラグインがゲーム起動時に作る設定ファイル。</summary>
    public const string PluginConfigFile = "BepInEx/config/io.github.utausnskareshi.bioincredemptionjp.cfg";

    /// <summary>BepInEx 5 が導入済みであることの目印になるファイル。</summary>
    public const string BepInEx5Marker = "BepInEx/core/BepInEx.dll";

    /// <summary>BepInEx 6(このパッチとは互換性が無い)が導入済みであることの目印になるファイル。</summary>
    public const string BepInEx6Marker = "BepInEx/core/BepInEx.Core.dll";

    /// <summary>BepInEx がゲームへ読み込まれるための入口になるDLL。他のMODローダーも同じ名前を使うことがある。</summary>
    public const string ProxyLibrary = "winhttp.dll";

    /// <summary>
    /// 配布物に必ず含まれていなければならない BepInEx 本体のファイル。1つでも欠けると BepInEx が起動せず、日本語にならない。
    /// (ウイルス対策ソフトが winhttp.dll を誤って隔離する例がある。欠けたまま導入して「完了」と表示しないために確認する)
    /// </summary>
    public static IReadOnlyList<string> RequiredLoaderFiles { get; } = new[]
    {
        ProxyLibrary,
        "doorstop_config.ini",
        "BepInEx/core/BepInEx.Preloader.dll",
        BepInEx5Marker,
        "BepInEx/core/0Harmony.dll",
    };

    /// <summary>配布物に必ず含まれていなければならない日本語化プラグインのファイル(太字フォントは無くても通常の太さで代用される)。</summary>
    public static IReadOnlyList<string> RequiredPluginFiles { get; } = new[]
    {
        PluginAssembly,
        PluginDirectory + "/BioIncRedemptionJP.Localization.dll",
        PluginDirectory + "/ja.xml",
        PluginDirectory + "/fonts/NotoSansJP-Medium.otf",
    };

    /// <summary>
    /// BepInEx がゲーム起動時に自動で作るファイルか(設定・ログ・キャッシュ)。
    /// これらは配布物には含まれないが、BepInEx をこのパッチが導入した場合は「元に戻す」で一緒に取り除く。
    /// </summary>
    public static bool IsGeneratedByLoader(string relativePath)
    {
        var path = RelativePaths.Normalize(relativePath);
        return RelativePaths.Comparer.Equals(path, "BepInEx/config/BepInEx.cfg")
            || RelativePaths.Comparer.Equals(path, PluginConfigFile)
            || path.StartsWith("BepInEx/LogOutput.log", StringComparison.OrdinalIgnoreCase)
            || RelativePaths.IsUnder(path, "BepInEx/cache");
    }

    /// <summary>日本語化プラグインのフォルダ内のファイルか。</summary>
    public static bool IsPluginFile(string relativePath)
    {
        return RelativePaths.IsUnder(relativePath, PluginDirectory);
    }
}

/// <summary>相対パスを安全に扱うための純粋関数。</summary>
public static class RelativePaths
{
    /// <summary>相対パス同士の比較方法(Windows のファイル名に合わせ、大文字小文字を区別しない)。</summary>
    public static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;

    /// <summary>区切りを '/' に統一し、前後の区切りを取り除く。</summary>
    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('\\', '/').Trim('/');
    }

    // Windows がファイルではなく装置として扱う名前(拡張子が付いていても装置を指す)
    private static readonly Regex ReservedDeviceName = new(
        @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9]|CONIN\$|CONOUT\$)(\..*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// 相対パスを、基準フォルダ配下の絶対パスへ直す。
    /// ".." を含むなど基準フォルダの外を指し得るパスは拒否する。
    /// (導入記録が壊れたり書き換えられたりしても、ゲームフォルダの外のファイルを消さないための安全策)
    /// </summary>
    /// <exception cref="PatchException">基準フォルダの外を指し得る不正なパスの場合。</exception>
    public static string ToFullPath(string root, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(root);

        var normalized = Normalize(relativePath);
        var segments = normalized.Split('/');
        var isUnsafe = normalized.Length == 0
            || normalized.Contains(':')
            || segments.Any(IsUnsafeSegment);
        if (isUnsafe)
        {
            throw new PatchException($"不正なパスが指定されました: \"{relativePath}\"");
        }

        var combined = Path.Combine(root, Path.Combine(segments));

        // 最後の確認として、Windows の正規化を通した後の場所が基準フォルダの内側(基準フォルダ自身は含まない)であることを確かめる。
        // 上の要素ごとの確認で防げない書き方があっても、外側や基準フォルダ自身を指すパスはここで止まる。
        var rootFull = Path.GetFullPath(root);
        if (!Path.EndsInDirectorySeparator(rootFull))
        {
            rootFull += Path.DirectorySeparatorChar;
        }

        if (!Path.GetFullPath(combined).StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new PatchException($"不正なパスが指定されました: \"{relativePath}\"");
        }

        return combined;
    }

    /// <summary>
    /// パスの要素として使うと、Windows の解釈で別の場所を指してしまう要素か。
    /// 末尾の「.」や空白は Windows が取り除くため、"x/..." や "x/.. " は x 自身を、"..." は基準フォルダ自身を指してしまう。
    /// また CON や NUL などはファイルではなく装置を指す。どれも配布物の正当なファイル名には現れない。
    /// </summary>
    private static bool IsUnsafeSegment(string segment)
    {
        return segment.Length == 0
            || segment == "."
            || segment == ".."
            || segment.EndsWith(".", StringComparison.Ordinal)
            || segment.EndsWith(" ", StringComparison.Ordinal)
            || ReservedDeviceName.IsMatch(segment);
    }

    /// <summary>親フォルダを浅い順に列挙する("a/b/c.txt" → "a", "a/b")。</summary>
    public static IEnumerable<string> Ancestors(string relativePath)
    {
        var segments = Normalize(relativePath).Split('/');
        return Enumerable.Range(1, Math.Max(0, segments.Length - 1))
            .Select(count => string.Join('/', segments.Take(count)));
    }

    /// <summary>パスが指定したフォルダの配下にあるか(フォルダ自身は含まない)。</summary>
    public static bool IsUnder(string relativePath, string directory)
    {
        var path = Normalize(relativePath);
        var prefix = Normalize(directory) + "/";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>階層の深さ(区切りの数)。</summary>
    public static int Depth(string relativePath)
    {
        return Normalize(relativePath).Count(c => c == '/');
    }
}

/// <summary>ファイルのハッシュ計算。</summary>
public static class FileHasher
{
    /// <summary>ファイルの SHA-256 を小文字の16進文字列で返す。</summary>
    public static string Sha256Hex(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
