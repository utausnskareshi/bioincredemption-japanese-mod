using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BioIncJp.Localization
{
    /// <summary>検証で見つかった問題の重さ。</summary>
    public enum ValidationSeverity
    {
        /// <summary>ゲーム内でエラーや表示崩れを起こす可能性が高く、必ず直すべきもの。</summary>
        Error,

        /// <summary>動作はするが、意図どおりでない可能性があるもの。</summary>
        Warning,

        /// <summary>参考情報(原文のまま残っている等)。</summary>
        Info,
    }

    /// <summary>検証で見つかった問題1件。</summary>
    /// <param name="Severity">重さ。</param>
    /// <param name="Code">問題の種類を表す短い識別子(集計や絞り込み用)。</param>
    /// <param name="Key">対象のリソースキー。</param>
    /// <param name="Message">人が読んで直せる説明。</param>
    public sealed record ValidationIssue(ValidationSeverity Severity, string Code, string Key, string Message);

    /// <summary>
    /// 翻訳の機械的な検証。副作用は無く、問題の一覧を返すだけ。
    /// ゲームは訳文を string.Format に渡したり、[arg] などの記号を別の文字列へ置き換えたりするため、
    /// 原文にある記号が訳文で欠けたり増えたりすると、例外や表示崩れの原因になる。
    /// </summary>
    public static class TranslationValidator
    {
        // {0} {1:0.0} のような書式指定子
        private static readonly Regex PlaceholderPattern = new Regex(@"\{(\d+)[^{}]*\}", RegexOptions.Compiled);

        // [arg] [arg2] [key:Chat] [1] のような、ゲームが実行時に置き換える記号
        private static readonly Regex BracketTokenPattern = new Regex(@"\[[^\[\]\s]{1,40}\]", RegexOptions.Compiled);

        // <b> </color> <size=20> <sprite name="x"> のようなリッチテキストタグ
        private static readonly Regex RichTextTagPattern = new Regex(@"<(/?)([A-Za-z][A-Za-z0-9\-]*)([^<>]*)>", RegexOptions.Compiled);

        // 閉じタグを伴わずに単独で使うタグ
        private static readonly HashSet<string> SelfClosingTags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "br", "sprite", "pos", "space", "indent", "line-indent", "page" };

        /// <summary>
        /// 原文と突き合わせて訳文を検証する。
        /// </summary>
        /// <param name="source">原文(キー → 文字列)。</param>
        /// <param name="translation">訳文(キー → 文字列)。</param>
        public static IReadOnlyList<ValidationIssue> ValidateAgainstSource(
            IReadOnlyDictionary<string, string> source,
            IReadOnlyDictionary<string, string> translation)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (translation is null)
            {
                throw new ArgumentNullException(nameof(translation));
            }

            var missing = source
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value) && !translation.ContainsKey(pair.Key))
                .Select(pair => new ValidationIssue(
                    ValidationSeverity.Warning, "missing", pair.Key, "訳がありません(原文のまま表示されます)。"));

            var unknown = translation.Keys
                .Where(key => !source.ContainsKey(key))
                .Select(key => new ValidationIssue(
                    ValidationSeverity.Warning, "unknown-key", key, "原文に存在しないキーです(ゲームの更新で削除された可能性があります)。"));

            var perEntry = translation
                .Where(pair => source.ContainsKey(pair.Key))
                .SelectMany(pair => CompareEntry(pair.Key, source[pair.Key], pair.Value));

            return missing.Concat(unknown).Concat(perEntry).Concat(ValidateStandalone(translation)).ToArray();
        }

        /// <summary>
        /// 原文無しでも分かる範囲の検証(タグの対応、波括弧の書式、不正な文字)。
        /// ゲーム本体のデータが無い環境(CIなど)でも実行できる。
        /// </summary>
        public static IReadOnlyList<ValidationIssue> ValidateStandalone(IReadOnlyDictionary<string, string> translation)
        {
            if (translation is null)
            {
                throw new ArgumentNullException(nameof(translation));
            }

            return translation.SelectMany(pair => CheckEntryAlone(pair.Key, pair.Value)).ToArray();
        }

        /// <summary>
        /// 原文が異なるのに訳文が同じになっている短い文言を探す。
        /// ゲームはダイアログのボタンを「表示文字列の一致」で判別している箇所があり、
        /// 同じダイアログの2つのボタンが同じ訳になると、押したボタンを取り違える。人の目で確認するための一覧。
        /// </summary>
        public static IReadOnlyList<ValidationIssue> FindShortCollisions(
            IReadOnlyDictionary<string, string> source,
            IReadOnlyDictionary<string, string> translation,
            int maxLength = 12)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (translation is null)
            {
                throw new ArgumentNullException(nameof(translation));
            }

            return translation
                .Where(pair => pair.Value.Length <= maxLength && source.ContainsKey(pair.Key))
                .GroupBy(pair => pair.Value.Trim().ToUpperInvariant(), StringComparer.Ordinal)
                .Where(group => group.Select(pair => source[pair.Key].Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count() > 1)
                .SelectMany(group => group.Select(pair => new ValidationIssue(
                    ValidationSeverity.Info,
                    "same-translation",
                    pair.Key,
                    $"原文の異なる複数のキーが同じ訳「{pair.Value}」になっています: {string.Join(", ", group.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))}")))
                .ToArray();
        }

        /// <summary>
        /// 訳文をゲームで使ったときに、書式の処理(string.Format)で例外を起こすかを調べる。問題があればその理由、無ければ null を返す。
        /// ゲームは多くの文言を string.Format(訳文, 引数) の形で表示し、引数は原文の書式指定子に合わせて渡している。
        /// そのため、原文に無い番号の書式指定子や、崩れた波括弧を含む訳文は、その画面で例外を起こす。
        /// ゲームの更新で原文から番号が消えた場合や、利用者が訳を書き換えた場合にも起こり得るため、読み込み時にも確かめる。
        /// </summary>
        /// <param name="source">書式の基準になる原文(通常は英語の原文。ゲームはこれに合わせて書式の引数を渡す)。</param>
        /// <param name="translated">訳文。</param>
        public static string? FindFormatHazard(string source, string translated)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (translated is null)
            {
                throw new ArgumentNullException(nameof(translated));
            }

            var extraIndexes = InNumericOrder(PlaceholderIndexes(translated).Except(PlaceholderIndexes(source)));
            if (extraIndexes.Count > 0)
            {
                return $"原文に無い書式指定子 {string.Join(", ", extraIndexes.Select(i => "{" + i + "}"))} があります";
            }

            // 原文の波括弧が正しい形なら、その文言は書式として使われている可能性がある。訳文の波括弧が崩れていると例外になる。
            // (原文自体が書式として正しくない文言は、ゲームが書式として使っていないので、訳文の波括弧も問題にならない)
            if (HasValidBraces(source) && !HasValidBraces(translated))
            {
                return "波括弧の書式が不正です";
            }

            return null;
        }

        /// <summary>1件分を原文と比較する。</summary>
        private static IEnumerable<ValidationIssue> CompareEntry(string key, string source, string translated)
        {
            // 書式指定子の番号は、原文と同じ集合でなければならない。
            // 原文に無い番号が訳にあると string.Format が例外を投げ、原文にある番号が訳に無いと情報が欠落する。
            var sourceIndexes = PlaceholderIndexes(source);
            var translatedIndexes = PlaceholderIndexes(translated);
            var extraIndexes = InNumericOrder(translatedIndexes.Except(sourceIndexes));
            var lostIndexes = InNumericOrder(sourceIndexes.Except(translatedIndexes));
            if (extraIndexes.Count > 0)
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Error, "placeholder-extra", key,
                    $"原文に無い書式指定子があります: {string.Join(", ", extraIndexes.Select(i => "{" + i + "}"))}");
            }

            if (lostIndexes.Count > 0)
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Error, "placeholder-missing", key,
                    $"原文にある書式指定子が訳にありません: {string.Join(", ", lostIndexes.Select(i => "{" + i + "}"))}");
            }

            // [arg] などの記号は、種類と個数が一致していなければならない
            var sourceTokens = CountTokens(BracketTokenPattern.Matches(source).Cast<Match>().Select(m => m.Value));
            var translatedTokens = CountTokens(BracketTokenPattern.Matches(translated).Cast<Match>().Select(m => m.Value));
            if (!SameCounts(sourceTokens, translatedTokens))
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Error, "bracket-token", key,
                    $"[ ] で囲まれた記号が原文と一致しません。原文: {Describe(sourceTokens)} / 訳: {Describe(translatedTokens)}");
            }

            // リッチテキストタグは、同じ種類を同じ数だけ使うのが基本(強調箇所を変えた等の意図的な差はあり得るので警告に留める)
            var sourceTags = CountTokens(TagNames(source));
            var translatedTags = CountTokens(TagNames(translated));
            if (!SameCounts(sourceTags, translatedTags))
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Warning, "rich-text-tag", key,
                    $"リッチテキストタグが原文と一致しません。原文: {Describe(sourceTags)} / 訳: {Describe(translatedTags)}");
            }

            // 文字として書かれた \n (バックスラッシュとn)は、ゲームが改行へ変換する
            var sourceBreaks = CountOccurrences(source, "\\n");
            var translatedBreaks = CountOccurrences(translated, "\\n");
            if (sourceBreaks != translatedBreaks)
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Info, "line-break", key,
                    $"改行記号 \\n の数が原文と異なります(原文 {sourceBreaks} / 訳 {translatedBreaks})。");
            }

            // 前後の空白は連結表示で意味を持つことがある
            if (LeadingWhitespace(source) != LeadingWhitespace(translated) || TrailingWhitespace(source) != TrailingWhitespace(translated))
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Warning, "edge-whitespace", key, "前後の空白が原文と異なります。");
            }

            if (string.Equals(source, translated, StringComparison.Ordinal) && source.Any(char.IsLetter))
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Info, "same-as-source", key, "訳が原文と同一です(固有名詞などで意図的なら問題ありません)。");
            }
        }

        /// <summary>1件分を単独で検査する。</summary>
        private static IEnumerable<ValidationIssue> CheckEntryAlone(string key, string translated)
        {
            if (!HasValidBraces(translated))
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Error, "brace-format", key,
                    "波括弧の書式が不正です。{0} のような書式指定子以外に { や } を使うと、ゲーム内で書式エラーになります。");
            }

            // 閉じタグが無いと、その装飾は文末まで続く。ゲームのテキスト描画(TextMeshPro)はこれを許容し、
            // 原文にも閉じタグの無い文言があるため、エラーではなく警告に留める(意図した書き方かを人が確認する)。
            var unbalanced = UnbalancedTags(translated);
            if (unbalanced.Count > 0)
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Warning, "tag-unbalanced", key,
                    $"開始タグと終了タグの数が合いません: {string.Join(", ", unbalanced)}");
            }

            if (translated.IndexOf('�') >= 0)
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Error, "replacement-char", key, "文字化けを示す文字(U+FFFD)が含まれています。");
            }

            if (translated.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Error, "control-char", key, "制御文字が含まれています。");
            }
        }

        /// <summary>
        /// 書式指定子の番号の集合。
        /// 番号は数値へ変換せず、先頭の 0 を除いた数字の並びのまま扱う("01" と "1" は同じ番号)。
        /// 数値へ変換すると、誤記で桁数が極端に多い番号があったときに変換が例外になり、
        /// 「問題として報告する」はずの検証そのものが止まってしまうため。
        /// </summary>
        private static ISet<string> PlaceholderIndexes(string text)
        {
            return new HashSet<string>(
                PlaceholderPattern.Matches(text).Cast<Match>().Select(m => NormalizeIndex(m.Groups[1].Value)),
                StringComparer.Ordinal);
        }

        /// <summary>番号の先頭の 0 を取り除く(すべて 0 の場合は "0")。</summary>
        private static string NormalizeIndex(string digits)
        {
            var trimmed = digits.TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }

        /// <summary>
        /// 番号を数値の小さい順に並べる。先頭の 0 を除いた数字の並びは、桁数が少ないほど小さく、同じ桁数なら文字順が数値順になる。
        /// </summary>
        private static IReadOnlyList<string> InNumericOrder(IEnumerable<string> indexes)
        {
            return indexes.OrderBy(index => index.Length).ThenBy(index => index, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// 波括弧が string.Format で解釈できる形になっているかを調べる。
        /// 許されるのは {数字...} の書式指定子と、{{ }} のエスケープだけ。
        /// </summary>
        private static bool HasValidBraces(string text)
        {
            var withoutEscapes = text.Replace("{{", string.Empty).Replace("}}", string.Empty);
            var withoutPlaceholders = PlaceholderPattern.Replace(withoutEscapes, string.Empty);
            return withoutPlaceholders.IndexOf('{') < 0 && withoutPlaceholders.IndexOf('}') < 0;
        }

        /// <summary>タグ名の列(開始タグは "b"、終了タグは "/b" の形)。</summary>
        private static IEnumerable<string> TagNames(string text)
        {
            return RichTextTagPattern.Matches(text).Cast<Match>()
                .Select(m => m.Groups[1].Value + m.Groups[2].Value.ToLowerInvariant());
        }

        /// <summary>開始と終了の数が合わないタグ名の一覧。</summary>
        private static IReadOnlyList<string> UnbalancedTags(string text)
        {
            var counts = CountTokens(TagNames(text));
            return counts.Keys
                .Select(name => name.TrimStart('/'))
                .Distinct(StringComparer.Ordinal)
                .Where(name => !SelfClosingTags.Contains(name))
                .Where(name => CountOf(counts, name) != CountOf(counts, "/" + name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static int CountOf(IReadOnlyDictionary<string, int> counts, string name)
        {
            return counts.TryGetValue(name, out var count) ? count : 0;
        }

        /// <summary>同じ値ごとの出現回数。</summary>
        private static IReadOnlyDictionary<string, int> CountTokens(IEnumerable<string> tokens)
        {
            return tokens.GroupBy(t => t, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        }

        private static bool SameCounts(IReadOnlyDictionary<string, int> left, IReadOnlyDictionary<string, int> right)
        {
            return left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var count) && count == pair.Value);
        }

        private static string Describe(IReadOnlyDictionary<string, int> counts)
        {
            return counts.Count == 0
                ? "(なし)"
                : string.Join(" ", counts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value == 1 ? p.Key : $"{p.Key}×{p.Value}"));
        }

        private static int CountOccurrences(string text, string token)
        {
            var count = 0;
            var index = text.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }

            return count;
        }

        private static string LeadingWhitespace(string text)
        {
            return new string(text.TakeWhile(char.IsWhiteSpace).ToArray());
        }

        private static string TrailingWhitespace(string text)
        {
            return new string(text.Reverse().TakeWhile(char.IsWhiteSpace).Reverse().ToArray());
        }
    }
}
