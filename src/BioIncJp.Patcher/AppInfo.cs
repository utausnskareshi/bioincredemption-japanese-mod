using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BioIncJp.Patcher;

/// <summary>このツール自身に関する情報。</summary>
internal static class AppInfo
{
    /// <summary>画面のタイトルなどに使う名前。</summary>
    public const string Title = "Bio Inc. Redemption 日本語化パッチ";

    /// <summary>
    /// 動作を確認したゲームのビルド番号(Steam の buildid)。
    /// これ以外のビルドでも適用はできるが、未確認である旨を画面に表示する。
    /// </summary>
    public static readonly IReadOnlyList<string> VerifiedGameBuilds = new[] { "25931977" };

    /// <summary>ツールの版("1.0.0" の形)。</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>
    /// 配布物(ゲームフォルダへ置く中身)のフォルダ。EXE と同じ場所の payload フォルダ。
    /// </summary>
    public static string DefaultPayloadDirectory => Path.Combine(AppContext.BaseDirectory, "payload");

    /// <summary>指定したビルドが動作確認済みか(ビルド番号が不明な場合は未確認として扱う)。</summary>
    public static bool IsVerifiedBuild(string buildId)
    {
        return VerifiedGameBuilds.Contains(buildId, StringComparer.Ordinal);
    }

    /// <summary>アセンブリに埋め込まれた版を読む。ビルド情報("+コミットID")が付いていれば取り除く。</summary>
    private static string ReadVersion()
    {
        var informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        var plusIndex = informational.IndexOf('+');
        return plusIndex >= 0 ? informational[..plusIndex] : informational;
    }
}
