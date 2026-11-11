using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace BioIncJp.Localization
{
    /// <summary>
    /// 翻訳ファイルの形式が不正だったことを表す例外。
    /// どのファイルの何行目で何が問題だったかを、利用者がそのまま直せる文面で伝える。
    /// </summary>
    public sealed class TranslationFormatException : Exception
    {
        public TranslationFormatException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 翻訳ファイル1つ分の読み込み結果(不変)。
    /// </summary>
    /// <param name="Entries">リソースキー → 訳文。空文字の訳は「未翻訳」として読み込み時に除外済み。</param>
    /// <param name="Warnings">読み込み自体は成功したが、直した方がよい点(重複キーなど)。</param>
    public sealed record TranslationCatalog(
        IReadOnlyDictionary<string, string> Entries,
        IReadOnlyList<string> Warnings)
    {
        /// <summary>訳が1件も無い空のカタログ。</summary>
        public static TranslationCatalog Empty { get; } =
            new TranslationCatalog(new Dictionary<string, string>(StringComparer.Ordinal), Array.Empty<string>());
    }

    /// <summary>
    /// 翻訳XML(ゲーム本体の言語ファイルと同じ形式)の読み込み。
    /// <code>
    /// &lt;resources&gt;
    ///   &lt;string name="Button.Close"&gt;閉じる&lt;/string&gt;
    /// &lt;/resources&gt;
    /// </code>
    /// 値の解釈はゲーム本体(XmlNode.InnerText)と揃えてあり、CDATA もそのまま連結される。
    /// ゲーム本体と違い、コメントは無視するので翻訳ファイルには自由に注釈を書ける。
    /// </summary>
    public static class TranslationXml
    {
        private const string RootElementName = "resources";
        private const string EntryElementName = "string";
        private const string KeyAttributeName = "name";

        /// <summary>ファイルから読み込む。存在しない・読めない・形式不正のいずれも、原因を含む例外にする。</summary>
        public static TranslationCatalog LoadFile(string path)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new TranslationFormatException($"翻訳ファイルが見つかりません: {path}");
            }

            try
            {
                // BOM の有無にかかわらず UTF-8 として読む(BOM があれば自動判定される)
                using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return Parse(reader, path);
            }
            catch (IOException ex)
            {
                throw new TranslationFormatException($"翻訳ファイルを読み込めません: {path} ({ex.Message})", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new TranslationFormatException($"翻訳ファイルへのアクセスが拒否されました: {path} ({ex.Message})", ex);
            }
        }

        /// <summary>文字列から読み込む(テストや検証ツール向け)。</summary>
        public static TranslationCatalog ParseText(string xml, string sourceName = "(文字列)")
        {
            if (xml is null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            using var reader = new StringReader(xml);
            return Parse(reader, sourceName);
        }

        /// <summary>
        /// 翻訳XMLを解析する。
        /// </summary>
        /// <param name="reader">XML本文。</param>
        /// <param name="sourceName">エラーメッセージに載せる出所(ファイルパスなど)。</param>
        public static TranslationCatalog Parse(TextReader reader, string sourceName)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var settings = new XmlReaderSettings
            {
                // 外部実体の展開は不要であり、安全のため禁止する
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                // 値の前後の空白や改行は意味を持ち得るので、ここでは捨てない
                IgnoreWhitespace = false,
            };

            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            var warnings = new List<string>();

            try
            {
                using var xml = XmlReader.Create(reader, settings);
                MoveToRoot(xml, sourceName);

                // ルート直下の要素を1つずつ読む
                var rootDepth = xml.Depth;
                if (xml.IsEmptyElement)
                {
                    return new TranslationCatalog(entries, warnings);
                }

                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.EndElement && xml.Depth == rootDepth)
                    {
                        break;
                    }

                    if (xml.NodeType != XmlNodeType.Element || xml.Depth != rootDepth + 1)
                    {
                        continue;
                    }

                    ReadEntry(xml, sourceName, entries, warnings);
                }
            }
            catch (XmlException ex)
            {
                throw new TranslationFormatException(
                    $"翻訳ファイルのXMLが壊れています: {sourceName} (行 {ex.LineNumber}, 列 {ex.LinePosition}) {ex.Message}", ex);
            }

            return new TranslationCatalog(entries, warnings);
        }

        /// <summary>ルート要素まで読み進め、想定どおりの要素名かを確認する。</summary>
        private static void MoveToRoot(XmlReader xml, string sourceName)
        {
            while (xml.Read())
            {
                if (xml.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!string.Equals(xml.LocalName, RootElementName, StringComparison.Ordinal))
                {
                    throw new TranslationFormatException(
                        $"翻訳ファイルのルート要素が <{RootElementName}> ではありません: {sourceName} (実際: <{xml.LocalName}>)");
                }

                return;
            }

            throw new TranslationFormatException($"翻訳ファイルにルート要素 <{RootElementName}> がありません: {sourceName}");
        }

        /// <summary>
        /// &lt;string name="..."&gt; 要素を1つ読み、辞書へ登録する。
        /// 読み終えた時点でリーダーはその要素の終了位置にある。
        /// </summary>
        private static void ReadEntry(
            XmlReader xml,
            string sourceName,
            Dictionary<string, string> entries,
            List<string> warnings)
        {
            var line = (xml as IXmlLineInfo)?.LineNumber ?? 0;

            if (!string.Equals(xml.LocalName, EntryElementName, StringComparison.Ordinal))
            {
                warnings.Add($"{sourceName} 行 {line}: 想定外の要素 <{xml.LocalName}> を無視しました。");
                // XmlReader.Skip() は次のノードまで進んでしまい、呼び出し元の Read() と合わせて1件読み飛ばす恐れがある。
                // 要素の終了位置で止まる ReadInnerText で中身だけを読み捨てる。
                _ = ReadInnerText(xml);
                return;
            }

            var key = xml.GetAttribute(KeyAttributeName);
            if (string.IsNullOrEmpty(key))
            {
                throw new TranslationFormatException(
                    $"翻訳ファイルの <{EntryElementName}> に {KeyAttributeName} 属性がありません: {sourceName} 行 {line}");
            }

            // ゲーム本体の InnerText と同じ結果になるよう、子孫のテキストとCDATAをすべて連結する
            var value = ReadInnerText(xml);

            // 空の訳は「まだ訳していない」という意味で扱い、原文のまま表示させる
            if (value.Length == 0)
            {
                return;
            }

            if (entries.ContainsKey(key!))
            {
                warnings.Add($"{sourceName} 行 {line}: キー \"{key}\" が重複しています。後に書かれた方を採用しました。");
            }

            entries[key!] = value;
        }

        /// <summary>現在の要素の中身を InnerText 相当の文字列として読む。</summary>
        private static string ReadInnerText(XmlReader xml)
        {
            if (xml.IsEmptyElement)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var depth = xml.Depth;
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth)
                {
                    break;
                }

                switch (xml.NodeType)
                {
                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        builder.Append(xml.Value);
                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 辞書を翻訳XMLの文字列へ書き出す(キーの並びは引数の順序どおり)。
        /// 翻訳の結合や検証ツールの出力で使う。
        /// </summary>
        public static string Serialize(IEnumerable<KeyValuePair<string, string>> entries)
        {
            if (entries is null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.None,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                OmitXmlDeclaration = false,
            };

            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, settings))
            {
                writer.WriteStartDocument(standalone: true);
                writer.WriteStartElement(RootElementName);
                foreach (var entry in entries)
                {
                    writer.WriteStartElement(EntryElementName);
                    writer.WriteAttributeString(KeyAttributeName, entry.Key);
                    writer.WriteString(entry.Value);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        }

        /// <summary>カタログに含まれる文字の種類を列挙する(フォントの事前生成に使う)。</summary>
        public static IReadOnlyCollection<char> CollectCharacters(TranslationCatalog catalog)
        {
            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            return catalog.Entries.Values.SelectMany(value => value).Distinct().ToArray();
        }
    }
}
