using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BioIncJp.Patcher.Core;
using BioIncJp.Patcher.Core.Steam;

namespace BioIncJp.Patcher;

// 起動引数の解釈(HeadlessAction / CommandLineOptions)は、画面に依存しない処理として中核ライブラリ(BioIncJp.Patcher.Core)にある。

/// <summary>
/// 画面を出さない実行(自動検証や、コマンドラインから操作したい利用者向け)。
/// 終了コード: 0 = 成功、1 = 操作に失敗、2 = ゲームが見つからない、3 = 引数の誤り。
/// </summary>
internal static class HeadlessRunner
{
    public const int ExitSuccess = 0;
    public const int ExitFailed = 1;
    public const int ExitGameNotFound = 2;
    public const int ExitInvalidArguments = 3;

    /// <summary>操作を実行し、終了コードを返す。出力は標準出力と(指定があれば)ファイルへ書く。</summary>
    public static int Run(CommandLineOptions options)
    {
        var lines = new List<string>();
        int exitCode;
        try
        {
            exitCode = Execute(options, lines);
        }
        catch (Exception ex)
        {
            // 画面なしの実行には、画面ありの実行のような「最後の受け皿」が無い。
            // 想定外の例外をそのままにすると、内容がどこにも残らないままプロセスが異常終了し、
            // 自動化スクリプトから原因を追えなくなる。種類を限定せずに受け、内容を結果として書き出す。
            lines.Add($"予期しないエラーが発生しました: {ex.GetType().Name}: {ex.Message}");
            lines.Add(ex.StackTrace ?? string.Empty);
            exitCode = ExitFailed;
        }

        Write(options.LogFile, lines);
        return exitCode;
    }

    private static int Execute(CommandLineOptions options, List<string> lines)
    {
        var detection = options.GameDirectory is null
            ? GameLocator.Locate(new RegistrySteamRootProvider())
            : GameLocator.FromDirectory(options.GameDirectory);

        if (detection is not GameDetectionResult.Found found)
        {
            lines.Add(((GameDetectionResult.NotFound)detection).Message);
            return ExitGameNotFound;
        }

        var game = found.Game;
        lines.Add($"ゲームの場所: {game.RootPath}");
        lines.Add($"ビルド番号: {(game.BuildId.Length > 0 ? game.BuildId : "(不明)")}");

        var service = new PatchService(options.PayloadDirectory ?? AppInfo.DefaultPayloadDirectory, AppInfo.Version);
        var progress = new SynchronousProgress(lines.Add);

        switch (options.Action)
        {
            case HeadlessAction.Status:
                var status = PatchService.GetStatus(game);
                lines.Add($"状態: {PatchScreenState.From(game, status).StatusText}");
                if (status.Note is not null)
                {
                    lines.Add(status.Note);
                }

                return ExitSuccess;

            case HeadlessAction.Apply:
                return Report(service.Apply(game, progress), lines);

            case HeadlessAction.Revert:
                return Report(service.Revert(game, progress), lines);

            default:
                lines.Add(CommandLineOptions.Usage);
                return ExitInvalidArguments;
        }
    }

    private static int Report(OperationResult result, List<string> lines)
    {
        lines.Add(result.Message);
        return result.Succeeded ? ExitSuccess : ExitFailed;
    }

    /// <summary>結果を標準出力へ書き、指定があればファイルにも書く。</summary>
    private static void Write(string? logFile, IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        if (logFile is null)
        {
            return;
        }

        try
        {
            File.WriteAllLines(logFile, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // 記録を書けなくても操作自体の結果は変わらないので、理由を標準エラーへ出して続行する。
            // ここで例外を外へ出すと、操作は済んでいるのにプロセスが異常終了し、終了コードが操作の結果と食い違ってしまう。
            // (パスとして使えない値は ArgumentException や NotSupportedException になる)
            Console.Error.WriteLine($"結果をファイルへ書き出せませんでした: {logFile} ({ex.Message})");
        }
    }

    /// <summary>報告を呼び出し元のスレッドでそのまま処理する IProgress(順序を保つため)。</summary>
    private sealed class SynchronousProgress : IProgress<string>
    {
        private readonly Action<string> _handler;

        public SynchronousProgress(Action<string> handler)
        {
            _handler = handler;
        }

        public void Report(string value) => _handler(value);
    }
}
