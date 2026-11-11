using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using UnityEngine;
using dryginstudios.commonscripts.resourcemanager;

namespace BioIncJp.Plugin
{
    /// <summary>
    /// ゲーム本体の非公開メンバーへのアクセスを1か所にまとめたもの。
    /// ゲームの更新でメンバー名が変わった場合は null を返し、呼び出し側が原因をログへ出せるようにする
    /// (ここで例外を握りつぶしたり、黙って何もしなかったりはしない)。
    /// </summary>
    internal static class GameAccess
    {
        /// <summary>ResourceManager が言語データを保持している辞書のフィールド名。</summary>
        public const string ContentFieldName = "content";

        /// <summary>ResourceManager が表示時の単語置換を保持しているリストのフィールド名。</summary>
        public const string WordOverridesFieldName = "WordsOverrides";

        // リフレクションの検索は起動中に1度だけ行う
        private static readonly FieldInfo? ContentField = AccessTools.Field(typeof(ResourceManager), ContentFieldName);
        private static readonly FieldInfo? WordOverridesField = AccessTools.Field(typeof(ResourceManager), WordOverridesFieldName);

        /// <summary>
        /// ゲームの言語辞書(キー → 表示文字列)。まだ読み込まれていない、または構造が変わっている場合は null。
        /// 返す辞書はゲームが実際に使っている実体なので、書き換えるとそのままゲームの表示に反映される。
        /// </summary>
        public static Dictionary<string, string>? GetLanguageContent()
        {
            return ContentField?.GetValue(null) as Dictionary<string, string>;
        }

        /// <summary>
        /// ゲームの単語置換リスト(置換前 → 置換後)。構造が変わっている場合は null。
        /// 返すリストはゲームが実際に使っている実体。
        /// </summary>
        public static List<KeyValuePair<string, string>>? GetWordOverrides()
        {
            return WordOverridesField?.GetValue(null) as List<KeyValuePair<string, string>>;
        }

        /// <summary>
        /// ゲームに同梱されている英語の言語データの場所。
        /// ゲームの ResourceFile.GetPathFromLanguage(SystemLanguage.English) が返すものと同じ(そのクラスは非公開のため直接は呼べない)。
        /// </summary>
        public const string EnglishLanguageResource = "Lang/en";

        /// <summary>
        /// ゲームに同梱されている英語の言語データ(キー → 文字列)を、ゲームと同じ方法で読む。見つからない場合は null。
        /// 訳文の書式の確認に使う(ゲームは英語の原文に合わせて書式の引数を渡すため)。
        /// </summary>
        /// <exception cref="XmlException">言語データが XML として壊れている場合。</exception>
        public static Dictionary<string, string>? LoadEnglishSource()
        {
            var asset = Resources.Load<TextAsset>(EnglishLanguageResource);
            if (asset == null)
            {
                return null;
            }

            var document = new XmlDocument();
            document.LoadXml(asset.text);
            if (document.DocumentElement is null)
            {
                return null;
            }

            // ゲームの ResourceManager.LoadCulture と同じく、各要素の name 属性をキー、InnerText を値にする(同じキーは後勝ち)
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                var name = node.Attributes?["name"]?.InnerText;
                if (!string.IsNullOrEmpty(name))
                {
                    result[name!] = node.InnerText;
                }
            }

            return result;
        }
    }
}
