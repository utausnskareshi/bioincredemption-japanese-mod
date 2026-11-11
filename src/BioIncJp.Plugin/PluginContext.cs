using System;
using System.IO;
using BepInEx.Logging;

namespace BioIncJp.Plugin
{
    /// <summary>プラグインの識別情報。BepInEx がプラグインを区別するために使う。</summary>
    internal static class PluginIdentity
    {
        /// <summary>一意な識別子。設定ファイル名(BepInEx\config\&lt;Guid&gt;.cfg)にもなる。</summary>
        public const string Guid = "io.github.utausnskareshi.bioincredemptionjp";

        /// <summary>ログに表示される名前。</summary>
        public const string Name = "BioIncRedemptionJP";
    }

    /// <summary>
    /// プラグイン全体で共有する実行時の情報(不変)。
    /// Harmony のフック処理は静的メソッドで書く必要があるため、起動時に1度だけ作ってこれを渡す。
    /// </summary>
    internal sealed class PluginContext
    {
        public PluginContext(ManualLogSource log, PluginSettings settings, string pluginDirectory)
        {
            Log = log ?? throw new ArgumentNullException(nameof(log));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        }

        /// <summary>BepInEx のログ出力(BepInEx\LogOutput.log に記録される)。</summary>
        public ManualLogSource Log { get; }

        /// <summary>設定値。</summary>
        public PluginSettings Settings { get; }

        /// <summary>このプラグインのDLLが置かれているフォルダ。翻訳ファイルやフォントもここに置く。</summary>
        public string PluginDirectory { get; }

        /// <summary>
        /// プラグインのフォルダからの相対パスを絶対パスへ直す。
        /// パスは利用者が設定ファイルに書く値なので、パスとして使えない文字が含まれている場合がある。
        /// その場合は例外にせず null を返し、原因を <paramref name="error"/> に入れる(呼び出し側がログへ出す)。
        /// </summary>
        public string? TryResolvePath(string relativePath, out string? error)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                error = "パスが空です。";
                return null;
            }

            try
            {
                error = null;
                return Path.GetFullPath(Path.Combine(PluginDirectory, relativePath));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                error = $"パスとして使えない値です: \"{relativePath}\" ({ex.Message})";
                return null;
            }
        }
    }
}
