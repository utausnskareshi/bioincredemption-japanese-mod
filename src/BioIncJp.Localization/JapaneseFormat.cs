using System;
using System.Collections.Generic;
using System.Globalization;

namespace BioIncJp.Localization
{
    /// <summary>
    /// 日本語表示用の整形処理(純粋関数のみ)。
    /// </summary>
    public static class JapaneseFormat
    {
        /// <summary>日本語表示のときに使うカルチャ名。日付や時刻の標準書式がこのカルチャに従う。</summary>
        public const string CultureName = "ja-JP";

        /// <summary>
        /// ゲーム内の日付(画面下部の日付表示や出来事の一覧)を「2026年11月10日」の形に整形する。
        /// 元のゲームは「10 Nov 2026」のように英語の月名を並べるため、日本語表示では置き換える。
        /// </summary>
        public static string FormatDate(DateTime date)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}年{1}月{2}日", date.Year, date.Month, date.Day);
        }

        /// <summary>
        /// キーの名前を、日本語の画面で分かりやすい表記にする。
        /// ゲームのキー設定画面は Unity の内部名(Alpha1、Keypad1、LeftControl など)をそのまま表示するため、
        /// 日本語表示のときは「1」「テンキー1」「左Ctrl」のように置き換える。対応表に無い名前(英字キーや F1 など)はそのまま返す。
        /// </summary>
        /// <param name="keyName">Unity のキーの内部名(KeyCode を文字列にしたもの)。</param>
        public static string DescribeKey(string keyName)
        {
            if (keyName is null)
            {
                throw new ArgumentNullException(nameof(keyName));
            }

            if (KeyNames.TryGetValue(keyName, out var japanese))
            {
                return japanese;
            }

            // 文字キーの上の段の数字キー(Alpha0〜Alpha9)は数字だけ、テンキー(Keypad0〜Keypad9)は「テンキー」を付ける
            if (keyName.Length == 6 && keyName.StartsWith("Alpha", StringComparison.Ordinal) && char.IsDigit(keyName[5]))
            {
                return keyName.Substring(5);
            }

            if (keyName.Length == 7 && keyName.StartsWith("Keypad", StringComparison.Ordinal) && char.IsDigit(keyName[6]))
            {
                return "テンキー" + keyName.Substring(6);
            }

            // マウスの4つ目以降のボタン(Mouse3 以降)は、ボタンの番号(1から数える)で表す
            if (keyName.StartsWith("Mouse", StringComparison.Ordinal)
                && int.TryParse(keyName.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var button)
                && button >= 3)
            {
                return string.Format(CultureInfo.InvariantCulture, "マウスボタン{0}", button + 1);
            }

            return keyName;
        }

        /// <summary>
        /// 名前の一覧を、日本語の読点「、」で区切ってつなぐ。
        /// ゲームは病名などを半角のコンマ「,」でつなぐため、日本語表示のときはこちらに置き換える。
        /// </summary>
        public static string JoinNames(IEnumerable<string> names)
        {
            if (names is null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            return string.Join("、", names);
        }

        // Unity のキーの内部名 → 日本語の画面での表記(数字キー・テンキーの数字・マウスの追加ボタンは DescribeKey の中で組み立てる)
        private static readonly Dictionary<string, string> KeyNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["None"] = "なし",
            ["Space"] = "スペース",
            ["Return"] = "Enter",
            ["Escape"] = "Esc",
            ["Backspace"] = "BackSpace",
            ["UpArrow"] = "↑",
            ["DownArrow"] = "↓",
            ["LeftArrow"] = "←",
            ["RightArrow"] = "→",
            ["LeftShift"] = "左Shift",
            ["RightShift"] = "右Shift",
            ["LeftControl"] = "左Ctrl",
            ["RightControl"] = "右Ctrl",
            ["LeftAlt"] = "左Alt",
            ["RightAlt"] = "右Alt",
            ["Mouse0"] = "左クリック",
            ["Mouse1"] = "右クリック",
            ["Mouse2"] = "ホイールクリック",
            ["KeypadPeriod"] = "テンキー.",
            ["KeypadDivide"] = "テンキー/",
            ["KeypadMultiply"] = "テンキー*",
            ["KeypadMinus"] = "テンキー-",
            ["KeypadPlus"] = "テンキー+",
            ["KeypadEnter"] = "テンキーEnter",
            ["KeypadEquals"] = "テンキー=",
            ["Comma"] = ",",
            ["Period"] = ".",
            ["Slash"] = "/",
            ["Backslash"] = "\\",
            ["Semicolon"] = ";",
            ["Quote"] = "'",
            ["LeftBracket"] = "[",
            ["RightBracket"] = "]",
            ["Minus"] = "-",
            ["Equals"] = "=",
            ["BackQuote"] = "`",
            ["Colon"] = ":",
            ["At"] = "@",
            ["Caret"] = "^",
            ["Underscore"] = "_",
            ["Numlock"] = "NumLock",
        };

        /// <summary>
        /// 翻訳確認用の表示。訳文の前にリソースキーを付け、画面上のどの文言がどのキーかを分かるようにする。
        /// </summary>
        public static string DecorateWithKey(string key, string value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return "[" + key + "] " + (value ?? string.Empty);
        }
    }
}
