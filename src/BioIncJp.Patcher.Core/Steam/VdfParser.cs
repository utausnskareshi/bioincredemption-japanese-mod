using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BioIncJp.Patcher.Core.Steam;

/// <summary>
/// Steam の設定ファイル(VDF / ACF。Valve 独自の「キーと値」形式)の1ノード。
/// 値を持つ葉か、子ノードを持つ枝のどちらかになる。
/// </summary>
/// <param name="Value">葉の場合の値。枝の場合は null。</param>
/// <param name="Children">枝の場合の子ノード(キーは大文字小文字を区別しない)。葉の場合は空。</param>
public sealed record VdfNode(string? Value, IReadOnlyDictionary<string, VdfNode> Children)
{
    private static readonly IReadOnlyDictionary<string, VdfNode> NoChildren =
        new Dictionary<string, VdfNode>(StringComparer.OrdinalIgnoreCase);

    /// <summary>値だけを持つ葉ノードを作る。</summary>
    public static VdfNode Leaf(string value) => new(value, NoChildren);

    /// <summary>子ノードを持つ枝ノードを作る。</summary>
    public static VdfNode Branch(IReadOnlyDictionary<string, VdfNode> children) => new(null, children);

    /// <summary>指定したキーの子ノード。無ければ null。</summary>
    public VdfNode? Child(string key) => Children.TryGetValue(key, out var child) ? child : null;

    /// <summary>指定したキーの子が葉であれば、その値。無い・枝である場合は null。</summary>
    public string? ChildValue(string key) => Child(key)?.Value;
}

/// <summary>VDF の書式が壊れていたことを表す例外。</summary>
public sealed class VdfFormatException : Exception
{
    public VdfFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// VDF / ACF の文字列を解析する。副作用は無く、同じ入力には常に同じ結果を返す。
///
/// 書式の例:
/// <code>
/// "libraryfolders"
/// {
///     "0"
///     {
///         "path"   "C:\\Program Files (x86)\\Steam"
///         "apps"   { "612470"  "1223376208" }
///     }
/// }
/// </code>
/// </summary>
public static class VdfParser
{
    /// <summary>
    /// 入れ子の深さの上限。Steam が書くファイルは数段しか入れ子にならない。
    /// 上限が無いと、壊れたファイル(開き括弧が延々と続くもの)の解析でスタックが溢れ、
    /// 例外として受け止めることもできないままツールが終了してしまう。
    /// </summary>
    public const int MaxDepth = 64;

    /// <summary>
    /// 文字列を解析し、最上位のキーを子に持つノードを返す。
    /// </summary>
    /// <exception cref="VdfFormatException">括弧の対応が取れていない、入れ子が深すぎるなど、書式が壊れている場合。</exception>
    public static VdfNode Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = Tokenize(text).ToArray();
        var position = 0;
        var root = ParseObject(tokens, ref position, isRoot: true, depth: 0);
        return root;
    }

    /// <summary>トークンの種類。</summary>
    private enum TokenKind
    {
        Text,
        OpenBrace,
        CloseBrace,
    }

    private readonly record struct Token(TokenKind Kind, string Text);

    /// <summary>
    /// 「キー 値」または「キー { ... }」の並びを読み、枝ノードにする。
    /// </summary>
    /// <param name="depth">この枝の入れ子の深さ(最上位は0)。</param>
    private static VdfNode ParseObject(Token[] tokens, ref int position, bool isRoot, int depth)
    {
        var children = new Dictionary<string, VdfNode>(StringComparer.OrdinalIgnoreCase);

        while (position < tokens.Length)
        {
            var token = tokens[position];
            if (token.Kind == TokenKind.CloseBrace)
            {
                if (isRoot)
                {
                    throw new VdfFormatException("対応する開き括弧の無い閉じ括弧 '}' があります。");
                }

                position++;
                return VdfNode.Branch(children);
            }

            if (token.Kind == TokenKind.OpenBrace)
            {
                throw new VdfFormatException("キーの無い開き括弧 '{' があります。");
            }

            var key = token.Text;
            position++;
            if (position >= tokens.Length)
            {
                throw new VdfFormatException($"キー \"{key}\" に対応する値がありません。");
            }

            var next = tokens[position];
            switch (next.Kind)
            {
                case TokenKind.OpenBrace:
                    if (depth >= MaxDepth)
                    {
                        throw new VdfFormatException($"入れ子が深すぎます({MaxDepth} 段まで)。ファイルが壊れている可能性があります。");
                    }

                    position++;
                    children[key] = ParseObject(tokens, ref position, isRoot: false, depth + 1);
                    break;
                case TokenKind.Text:
                    position++;
                    children[key] = VdfNode.Leaf(next.Text);
                    break;
                default:
                    throw new VdfFormatException($"キー \"{key}\" の直後に閉じ括弧 '}}' があります。");
            }
        }

        if (!isRoot)
        {
            throw new VdfFormatException("閉じ括弧 '}' が足りません。");
        }

        return VdfNode.Branch(children);
    }

    /// <summary>文字列をトークン(文字列・開き括弧・閉じ括弧)へ分解する。</summary>
    private static IEnumerable<Token> Tokenize(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            var c = text[index];

            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }

            // 行コメント
            if (c == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }

                continue;
            }

            if (c == '{')
            {
                index++;
                yield return new Token(TokenKind.OpenBrace, "{");
                continue;
            }

            if (c == '}')
            {
                index++;
                yield return new Token(TokenKind.CloseBrace, "}");
                continue;
            }

            if (c == '"')
            {
                yield return new Token(TokenKind.Text, ReadQuoted(text, ref index));
                continue;
            }

            yield return new Token(TokenKind.Text, ReadBare(text, ref index));
        }
    }

    /// <summary>引用符で囲まれた文字列を読む。\\ や \" などのエスケープを解釈する。</summary>
    private static string ReadQuoted(string text, ref int index)
    {
        var builder = new StringBuilder();
        index++; // 開きの引用符

        while (index < text.Length)
        {
            var c = text[index];
            if (c == '"')
            {
                index++;
                return builder.ToString();
            }

            if (c == '\\' && index + 1 < text.Length)
            {
                var escaped = text[index + 1];
                switch (escaped)
                {
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case '\\':
                    case '"':
                        builder.Append(escaped);
                        break;
                    default:
                        // 決められたエスケープ以外(\S など)は、バックスラッシュも含めてそのまま残す。
                        // エスケープされていない Windows のパス("D:\SteamLibrary")からバックスラッシュが消えるのを防ぐ。
                        builder.Append('\\').Append(escaped);
                        break;
                }

                index += 2;
                continue;
            }

            builder.Append(c);
            index++;
        }

        throw new VdfFormatException("引用符 '\"' が閉じられていません。");
    }

    /// <summary>引用符の無い文字列を、空白か括弧が来るまで読む。</summary>
    private static string ReadBare(string text, ref int index)
    {
        var start = index;
        while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != '{' && text[index] != '}' && text[index] != '"')
        {
            index++;
        }

        return text[start..index];
    }
}
