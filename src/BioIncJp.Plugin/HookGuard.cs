using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace BioIncJp.Plugin
{
    /// <summary>
    /// フック(ゲームのメソッドへ差し込んだ処理)の中で起きた想定外の例外を受け止める。
    ///
    /// フックはゲーム本体のメソッドの一部として実行されるため、ここから例外が飛び出すと、
    /// ゲームの起動処理や文字の描画が途中で止まってしまう。日本語化の不具合でゲーム自体を壊さないよう、
    /// 例外は機能名と内容をログへ記録したうえで、ゲームの処理はそのまま続けさせる。
    /// </summary>
    internal static class HookGuard
    {
        private static ManualLogSource? _log;

        // 詳細を記録済みの機能。文字を描くたびに呼ばれるフックもあるので、同じ機能の例外を毎回出すとログが埋まってしまう。
        private static readonly HashSet<string> ReportedFeatures = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>起動時に1度だけ呼ぶ。</summary>
        public static void Initialize(ManualLogSource log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// フックの中で捕まえた例外を記録する。同じ機能については、最初の1回だけ詳細を出す。
        /// </summary>
        /// <param name="feature">利用者に分かる機能名(例: 「翻訳の適用」)。</param>
        /// <param name="exception">捕まえた例外。</param>
        public static void Report(string feature, Exception exception)
        {
            if (!ReportedFeatures.Add(feature))
            {
                return;
            }

            _log?.LogError(
                $"機能「{feature}」の処理中にエラーが発生しました。ゲームは続行しますが、この機能は正しく働かない可能性があります。" +
                $"(同じ機能のエラーは、以後は記録しません)\n{exception}");
        }
    }
}
