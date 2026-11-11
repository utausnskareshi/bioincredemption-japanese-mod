using System;
using System.Collections.Generic;

namespace BioIncJp.Patcher.Core;

/// <summary>画面を出さずに実行する場合の操作。</summary>
public enum HeadlessAction
{
    /// <summary>画面を出す(通常の起動)。</summary>
    None,

    /// <summary>状態を表示する。</summary>
    Status,

    /// <summary>日本語化する。</summary>
    Apply,

    /// <summary>元に戻す。</summary>
    Revert,
}

/// <summary>
/// 起動時の引数の解釈結果(不変)。
/// 解釈は副作用の無い処理なので、画面を持たないこのライブラリに置いている。
/// </summary>
/// <param name="Action">画面を出さずに行う操作。通常の起動なら None。</param>
/// <param name="GameDirectory">ゲームのフォルダを直接指定する場合のパス(自動検出を使わない)。</param>
/// <param name="PayloadDirectory">配布物のフォルダを直接指定する場合のパス。</param>
/// <param name="LogFile">実行結果を書き出すファイル。</param>
/// <param name="Error">引数に誤りがある場合の説明。無ければ null。</param>
public sealed record CommandLineOptions(
    HeadlessAction Action,
    string? GameDirectory,
    string? PayloadDirectory,
    string? LogFile,
    string? Error)
{
    /// <summary>引数の使い方の説明。</summary>
    public const string Usage =
        "使い方: BioIncRedemptionJP.exe [--status | --apply | --revert] [--game-dir <ゲームのフォルダ>] [--payload <配布物のフォルダ>] [--log <出力ファイル>]\n" +
        "  引数なし   画面を表示します。\n" +
        "  --status   日本語化の状態を表示します。\n" +
        "  --apply    日本語化します。\n" +
        "  --revert   元に戻します。\n" +
        "  --game-dir Steam からの自動検出を使わず、ゲームのフォルダを指定します。\n" +
        "  --payload  配布物のフォルダを指定します(既定は EXE と同じ場所の payload)。\n" +
        "  --log      結果をファイルへ書き出します(画面なしの実行では、標準出力が見えない場合があるため)。";

    /// <summary>引数を解釈する(副作用なし)。</summary>
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var action = HeadlessAction.None;
        string? gameDirectory = null;
        string? payloadDirectory = null;
        string? logFile = null;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg.ToLowerInvariant())
            {
                case "--status":
                case "--apply":
                case "--revert":
                    var requested = ToAction(arg);
                    // 「日本語化」と「元に戻す」のように別々の操作を同時に指定された場合は、どちらを行うべきか判断できない。
                    // 後ろの指定を黙って採用すると、意図と逆の操作をしてしまう恐れがあるため、誤りとして扱う。
                    if (action != HeadlessAction.None && action != requested)
                    {
                        return Invalid("--status / --apply / --revert は、どれか1つだけを指定してください。");
                    }

                    action = requested;
                    break;
                case "--game-dir":
                case "--payload":
                case "--log":
                    // 値が無い、または次の引数(-- で始まるもの)が続いている場合は、値の指定し忘れとみなす
                    if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        return Invalid($"{arg} の後に値が必要です。");
                    }

                    var value = args[++index];
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        // 空のパスは、後でファイルやフォルダを開くときに想定外の例外になるため、ここで止める
                        return Invalid($"{arg} の値が空です。");
                    }

                    if (arg.Equals("--game-dir", StringComparison.OrdinalIgnoreCase))
                    {
                        gameDirectory = value;
                    }
                    else if (arg.Equals("--payload", StringComparison.OrdinalIgnoreCase))
                    {
                        payloadDirectory = value;
                    }
                    else
                    {
                        logFile = value;
                    }

                    break;
                default:
                    return Invalid($"不明な引数です: {arg}");
            }
        }

        return new CommandLineOptions(action, gameDirectory, payloadDirectory, logFile, Error: null);
    }

    /// <summary>操作の引数を操作の種類へ変換する(呼び出し側で操作の引数であることを確認済み)。</summary>
    private static HeadlessAction ToAction(string arg)
    {
        return arg.ToLowerInvariant() switch
        {
            "--status" => HeadlessAction.Status,
            "--apply" => HeadlessAction.Apply,
            "--revert" => HeadlessAction.Revert,
            _ => throw new ArgumentOutOfRangeException(nameof(arg), arg, "操作を表す引数ではありません。"),
        };
    }

    private static CommandLineOptions Invalid(string message)
    {
        return new CommandLineOptions(HeadlessAction.None, null, null, null, message);
    }
}
