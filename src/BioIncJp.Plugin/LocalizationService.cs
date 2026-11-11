using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml;
using BioIncJp.Localization;
using dryginstudios.bioinc.gamedata;
using dryginstudios.commonscripts.resourcemanager;

namespace BioIncJp.Plugin
{
    /// <summary>
    /// 翻訳をゲームへ反映する処理の窓口。
    /// 「何をどう置き換えるか」の判断は副作用の無い <see cref="TranslationOverlay"/> に任せ、
    /// ここではファイル読み込みとゲームの辞書の書き換えという副作用だけを扱う。
    /// </summary>
    internal static class LocalizationService
    {
        /// <summary>ログへ列挙する未翻訳キーの上限(多すぎるとログが読めなくなるため)。</summary>
        private const int MaxListedKeys = 40;

        /// <summary>
        /// 画面に表示する文言ではなく、ゲームの動作を制御するためのキー。翻訳の対象ではないので、未翻訳として報告しない。
        /// (Words.Override は「表示時に行う単語の置換」の定義)
        /// </summary>
        private static readonly HashSet<string> ControlKeys = new HashSet<string>(StringComparer.Ordinal) { "Words.Override" };

        private static PluginContext? _context;

        // 読み込み済みの翻訳。null のときは次に必要になった時点でファイルから読む。
        private static TranslationCatalog? _catalog;

        // 訳文の書式の確認に使う英語の原文。null のときはまだ読んでいない。
        // ゲームの起動中は変わらないので1度だけ読む(読めなかった場合は空の辞書を入れ、毎回読み直さない)。
        private static IReadOnlyDictionary<string, string>? _formatReference;

        /// <summary>起動時に1度だけ呼ぶ。</summary>
        public static void Initialize(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 日本語表示が実際に働いているか。設定で有効になっていて、かつ翻訳を読み込めた場合だけ true になる。
        /// 翻訳ファイルが無い・壊れている場合は false とし、日付の表記なども含めてゲームを元の表示のままにする
        /// (文言は英語なのに日付だけ日本語、という中途半端な状態にしないため)。
        /// </summary>
        public static bool IsJapaneseActive => _context?.Settings.Enabled == true && Catalog.Entries.Count > 0;

        /// <summary>読み込み済みの翻訳(未読み込みならここで読む)。</summary>
        public static TranslationCatalog Catalog => _catalog ??= LoadCatalog();

        /// <summary>
        /// ゲームが言語データを読み込んだ直後に呼ばれ、その辞書へ日本語訳を重ねる。
        /// 訳の無いキーは、ゲームが読み込んだ元の言語のまま残る。
        /// </summary>
        public static void ApplyToGame()
        {
            var context = _context;
            if (context is null || !IsJapaneseActive)
            {
                // 無効な場合と、翻訳を読み込めなかった場合(原因は読み込み時に記録済み)は、ゲームの辞書に一切触れない
                return;
            }

            var content = GameAccess.GetLanguageContent();
            if (content is null)
            {
                context.Log.LogError(
                    $"ゲームの言語辞書(ResourceManager.{GameAccess.ContentFieldName})を取得できないため、日本語訳を適用できません。" +
                    "ゲームの更新で内部構造が変わった可能性があります。");
                return;
            }

            var gameOverrides = GameAccess.GetWordOverrides();
            if (gameOverrides is null)
            {
                context.Log.LogWarning(
                    $"ゲームの単語置換リスト(ResourceManager.{GameAccess.WordOverridesFieldName})を取得できません。" +
                    "訳文の一部の単語が、ゲーム側の置換で書き換わる可能性があります。");
            }

            // 判断は純粋関数で行い、結果をまとめて反映する
            var overrides = gameOverrides?.ToArray() ?? Array.Empty<KeyValuePair<string, string>>();
            var formatReference = _formatReference ??= LoadFormatReference(context);
            var result = TranslationOverlay.Apply(new Dictionary<string, string>(content), Catalog.Entries, overrides, formatReference);

            content.Clear();
            foreach (var pair in result.Content)
            {
                content[pair.Key] = pair.Value;
            }

            // 単語置換は未翻訳の原文へ適用済みなので、訳文が書き換えられないようゲーム側のリストは空にする
            gameOverrides?.Clear();

            ReportResult(context, result);
        }

        /// <summary>
        /// 日本語表示のときに、日付や時刻の標準書式が日本語になるようスレッドのカルチャを切り替える。
        /// ゲームは言語読み込みの直後にカルチャを英語などへ設定するので、その後に呼ぶ。
        /// </summary>
        public static void ApplyJapaneseCulture()
        {
            var context = _context;
            if (context is null || !IsJapaneseActive)
            {
                return;
            }

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(JapaneseFormat.CultureName);
            }
            catch (CultureNotFoundException ex)
            {
                // カルチャ情報が無い環境でも翻訳表示自体は動くので、原因を記録して続行する
                context.Log.LogWarning($"カルチャ {JapaneseFormat.CultureName} を設定できませんでした。日付の書式は元のままになります: {ex.Message}");
            }
        }

        /// <summary>
        /// 翻訳ファイルを読み込み直してゲームへ反映する(翻訳作業用)。
        /// ゲームの言語読み込みをもう一度実行させることで、通常の起動時と同じ経路を通す。
        /// </summary>
        public static void ReloadFromDisk()
        {
            var context = _context;
            if (context is null)
            {
                return;
            }

            _catalog = null;
            try
            {
                ResourceManager.LoadCulture(GameData.Current.GameSettings.Culture);
                context.Log.LogInfo("翻訳ファイルを読み込み直しました。");
            }
            catch (NotSupportedException ex)
            {
                // GameData.Current は、セーブデータの読み込み前に参照するとこの例外を投げる
                context.Log.LogWarning($"ゲームのデータがまだ読み込まれていないため、再読み込みできません: {ex.Message}");
            }
        }

        /// <summary>リソースキー表示が有効なら、訳文の前にキーを付ける。</summary>
        public static string DecorateIfDebug(string key, string value)
        {
            var context = _context;
            if (context is null || !context.Settings.ShowKeys || string.IsNullOrEmpty(value))
            {
                return value;
            }

            return JapaneseFormat.DecorateWithKey(key, value);
        }

        /// <summary>翻訳ファイルを読む。失敗した場合は原因を記録し、空の翻訳(=すべて原文表示)として続行する。</summary>
        private static TranslationCatalog LoadCatalog()
        {
            var context = _context;
            if (context is null)
            {
                return TranslationCatalog.Empty;
            }

            var path = context.TryResolvePath(context.Settings.TranslationFile, out var pathError);
            if (path is null)
            {
                context.Log.LogError($"設定 TranslationFile が不正なため、原文のまま表示します。{pathError}");
                return TranslationCatalog.Empty;
            }

            try
            {
                var catalog = TranslationXml.LoadFile(path);
                foreach (var warning in catalog.Warnings.Take(MaxListedKeys))
                {
                    context.Log.LogWarning(warning);
                }

                context.Log.LogInfo($"翻訳ファイルを読み込みました: {path} ({catalog.Entries.Count} 件)");
                return catalog;
            }
            catch (TranslationFormatException ex)
            {
                context.Log.LogError($"翻訳ファイルを読み込めなかったため、原文のまま表示します。{ex.Message}");
                return TranslationCatalog.Empty;
            }
        }

        /// <summary>
        /// 訳文の書式の確認に使う英語の原文を読む。
        /// ゲームの言語をロシア語などにしていると、その言語の原文には書式指定子が省かれていることがあり、
        /// それと比べると問題の無い訳まで不採用になってしまう。ゲームは英語の原文に合わせて引数を渡すので、英語と比べる。
        /// 読めない場合は理由を記録して空の辞書を返す(その場合は、現在の言語の原文と比べる)。
        /// </summary>
        private static IReadOnlyDictionary<string, string> LoadFormatReference(PluginContext context)
        {
            try
            {
                var english = GameAccess.LoadEnglishSource();
                if (english is not null)
                {
                    return english;
                }

                context.Log.LogWarning(
                    $"英語の言語データ({GameAccess.EnglishLanguageResource})が見つからないため、訳文の書式は現在の言語の原文と比べて確かめます。");
            }
            catch (XmlException ex)
            {
                context.Log.LogWarning($"英語の言語データを読み込めないため、訳文の書式は現在の言語の原文と比べて確かめます: {ex.Message}");
            }

            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>適用結果をログへ出す。未翻訳キーは、ゲーム更新で文言が増えたときの手掛かりになる。</summary>
        private static void ReportResult(PluginContext context, OverlayResult result)
        {
            var untranslated = result.UntranslatedKeys.Where(key => !ControlKeys.Contains(key)).ToArray();
            context.Log.LogInfo(
                $"日本語訳を適用しました: {result.TranslatedCount} 件 (未翻訳 {untranslated.Length} 件、" +
                $"書式が合わず不採用 {result.RejectedTranslations.Count} 件、ゲームに無いキー {result.UnknownKeys.Count} 件)");

            if (untranslated.Length > 0)
            {
                var listed = string.Join(", ", untranslated.Take(MaxListedKeys));
                var suffix = untranslated.Length > MaxListedKeys ? " ..." : string.Empty;
                context.Log.LogInfo($"未翻訳のキー(原文のまま表示されます): {listed}{suffix}");
            }

            if (result.RejectedTranslations.Count > 0)
            {
                // ゲームの更新で原文の書式が変わったか、訳が書き換えられた可能性がある。使うと画面で例外になるため原文を表示している
                var listed = string.Join(", ", result.RejectedTranslations.Take(MaxListedKeys).Select(rejected => $"{rejected.Key}({rejected.Reason})"));
                var suffix = result.RejectedTranslations.Count > MaxListedKeys ? " ..." : string.Empty;
                context.Log.LogWarning($"ゲームの原文と書式が合わないため、次の訳は使わずに原文のまま表示します: {listed}{suffix}");
            }
        }
    }
}
