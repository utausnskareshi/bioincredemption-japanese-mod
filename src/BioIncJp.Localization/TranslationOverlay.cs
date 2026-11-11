using System;
using System.Collections.Generic;
using System.Linq;

namespace BioIncJp.Localization
{
    /// <summary>
    /// 原文の辞書へ訳文を重ねた結果(不変)。
    /// </summary>
    /// <param name="Content">ゲームへ渡す最終的な辞書(キー → 表示文字列)。</param>
    /// <param name="TranslatedCount">訳文に置き換わったキーの数。</param>
    /// <param name="UntranslatedKeys">原文にはあるが訳が無く、原文のまま表示されるキー。</param>
    /// <param name="UnknownKeys">訳にはあるが原文に無いキー(ゲームの更新で消えた可能性がある)。</param>
    /// <param name="RejectedTranslations">原文と書式が合わず、ゲームで例外を起こすおそれがあるため採用しなかった訳(原文のまま表示される)。</param>
    public sealed record OverlayResult(
        IReadOnlyDictionary<string, string> Content,
        int TranslatedCount,
        IReadOnlyList<string> UntranslatedKeys,
        IReadOnlyList<string> UnknownKeys,
        IReadOnlyList<RejectedTranslation> RejectedTranslations);

    /// <summary>採用しなかった訳1件。</summary>
    /// <param name="Key">リソースキー。</param>
    /// <param name="Reason">採用しなかった理由(ログへ出す説明)。</param>
    public sealed record RejectedTranslation(string Key, string Reason);

    /// <summary>
    /// ゲームが読み込んだ言語辞書へ日本語訳を重ねる処理。副作用は無く、入力から新しい辞書を作って返す。
    /// </summary>
    public static class TranslationOverlay
    {
        /// <summary>
        /// 原文辞書に訳文を重ねる。
        /// </summary>
        /// <param name="source">ゲームが読み込んだ元の言語の辞書。</param>
        /// <param name="translation">日本語訳。</param>
        /// <param name="wordOverrides">
        /// ゲーム本体が表示時に行う単語置換(英語なら Click→Tap など)。
        /// 訳文に対しては行わず、訳が無く原文のまま残るキーにだけ先に適用しておく。
        /// こうすると、呼び出し側でゲームの置換リストを空にしても、未翻訳部分の見た目は元のゲームと変わらない。
        /// </param>
        /// <param name="formatReference">
        /// 訳文の書式を確かめるときに比べる原文(キー → 文字列)。通常は英語の原文を渡す。
        /// ゲームは英語の原文に合わせて書式の引数を渡すため、他の言語(翻訳者が書式指定子を省いていることがある)を基にしているときも、
        /// 英語の原文と比べないと、問題の無い訳まで不採用にしてしまう。
        /// 省略した場合や、キーが無い・空の場合は <paramref name="source"/> の原文と比べる。
        /// </param>
        public static OverlayResult Apply(
            IReadOnlyDictionary<string, string> source,
            IReadOnlyDictionary<string, string> translation,
            IReadOnlyList<KeyValuePair<string, string>> wordOverrides,
            IReadOnlyDictionary<string, string>? formatReference = null)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (translation is null)
            {
                throw new ArgumentNullException(nameof(translation));
            }

            if (wordOverrides is null)
            {
                throw new ArgumentNullException(nameof(wordOverrides));
            }

            var content = new Dictionary<string, string>(source.Count + translation.Count, StringComparer.Ordinal);
            var untranslated = new List<string>();
            var rejected = new List<RejectedTranslation>();
            var translatedCount = 0;

            foreach (var pair in source)
            {
                // 訳があっても、ゲームの原文と書式が合わない訳は使わない(使うと、その文言を表示する画面で例外になる)
                var reference = formatReference is not null
                    && formatReference.TryGetValue(pair.Key, out var referenceText)
                    && !string.IsNullOrEmpty(referenceText)
                    ? referenceText
                    : pair.Value;
                var hazard = translation.TryGetValue(pair.Key, out var translated) && translated.Length > 0
                    ? TranslationValidator.FindFormatHazard(reference, translated)
                    : null;

                if (translated is { Length: > 0 } && hazard is null)
                {
                    content[pair.Key] = translated;
                    translatedCount++;
                }
                else if (hazard is not null)
                {
                    content[pair.Key] = ApplyWordOverrides(pair.Value, wordOverrides);
                    rejected.Add(new RejectedTranslation(pair.Key, hazard));
                }
                else
                {
                    content[pair.Key] = ApplyWordOverrides(pair.Value, wordOverrides);
                    // 原文が空のキーは訳す対象ではないので、未翻訳としては数えない
                    if (!string.IsNullOrWhiteSpace(pair.Value))
                    {
                        untranslated.Add(pair.Key);
                    }
                }
            }

            // 原文に無いキーも辞書には入れておく(将来のゲーム更新で使われる可能性があり、入れても害が無い)
            var unknown = translation.Keys.Where(key => !source.ContainsKey(key)).ToArray();
            foreach (var key in unknown)
            {
                content[key] = translation[key];
            }

            return new OverlayResult(content, translatedCount, untranslated, unknown, rejected);
        }

        /// <summary>ゲーム本体と同じ順序・同じ方法(単純な文字列置換)で単語置換を適用する。</summary>
        public static string ApplyWordOverrides(string value, IReadOnlyList<KeyValuePair<string, string>> wordOverrides)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (wordOverrides is null)
            {
                throw new ArgumentNullException(nameof(wordOverrides));
            }

            return wordOverrides.Aggregate(
                value,
                (current, pair) => string.IsNullOrEmpty(pair.Key) ? current : current.Replace(pair.Key, pair.Value));
        }
    }
}
