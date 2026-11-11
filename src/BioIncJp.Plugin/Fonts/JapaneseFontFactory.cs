using System;
using System.IO;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace BioIncJp.Plugin.Fonts
{
    /// <summary>
    /// 日本語フォントの生成条件(不変)。
    /// </summary>
    /// <param name="SamplingPointSize">フォントを画像化するときの文字サイズ(ピクセル)。</param>
    /// <param name="AtlasPadding">文字の周囲に確保する余白(ピクセル)。縁取りや光彩の描画に使われる。</param>
    /// <param name="AtlasSize">文字を並べる画像1枚の一辺(ピクセル)。</param>
    /// <param name="LineHeightScale">行の高さの倍率(ゲーム標準フォント比)。</param>
    internal sealed record FontCreationOptions(int SamplingPointSize, int AtlasPadding, int AtlasSize, float LineHeightScale);

    /// <summary>
    /// フォントファイル(TTF/OTF)から、TextMeshPro 用のフォントアセットを実行時に作る。
    /// 作るのは「必要になった文字をその場で画像化して追加していく」動的アセットで、
    /// 日本語のように文字数が多い言語でも、使った文字の分だけメモリを消費する。
    /// </summary>
    internal static class JapaneseFontFactory
    {
        // ゲーム標準フォント(Exo 2)の行の寸法を、文字サイズに対する比で表したもの。
        // 日本語フォントの寸法をこれに合わせることで、日本語が混ざった行でも行間や縦位置が元のレイアウトと変わらない。
        // (Noto Sans JP 本来の寸法は上下に大きく、そのままだと行間が2割ほど広がって枠からはみ出しやすい)
        private const float BaseAscentRatio = 1.0f;
        private const float BaseDescentRatio = -0.2f;
        private const float BaseLineHeightRatio = 1.2f;

        /// <summary>
        /// フォントアセットを作る。失敗した場合は原因をログへ出して null を返す。
        /// </summary>
        /// <param name="fontFilePath">フォントファイルの絶対パス。</param>
        /// <param name="assetName">作成するアセットの名前(ログや診断で区別するためのもの)。</param>
        /// <param name="options">生成条件。</param>
        /// <param name="log">ログ出力先。</param>
        public static TMP_FontAsset? Create(string fontFilePath, string assetName, FontCreationOptions options, ManualLogSource log)
        {
            if (!File.Exists(fontFilePath))
            {
                log.LogError($"フォントファイルが見つかりません: {fontFilePath}");
                return null;
            }

            try
            {
                // パス指定でフォントを読み込む。Unity はこのときフォントのデータを保持するため、
                // 後から文字を追加で画像化できる(OSにインストールされたフォントを名前で開く方法では、これができない)
                var font = new Font(fontFilePath);
                font.hideFlags = HideFlags.HideAndDontSave;

                var asset = TMP_FontAsset.CreateFontAsset(
                    font,
                    options.SamplingPointSize,
                    options.AtlasPadding,
                    GlyphRenderMode.SDFAA,
                    options.AtlasSize,
                    options.AtlasSize,
                    AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);

                if (asset == null)
                {
                    log.LogError($"フォントを読み込めませんでした(対応していない形式か、ファイルが壊れている可能性があります): {fontFilePath}");
                    return null;
                }

                asset.name = assetName;
                // シーンの切り替えや未使用アセットの解放で破棄されないようにする
                asset.hideFlags = HideFlags.HideAndDontSave;
                if (asset.material != null)
                {
                    asset.material.name = assetName + " Material";
                    asset.material.hideFlags = HideFlags.HideAndDontSave;
                }

                asset.faceInfo = AlignLineMetrics(asset.faceInfo, options.LineHeightScale);
                return asset;
            }
            catch (Exception ex)
            {
                // Unity 側の失敗(シェーダーが見つからない等)は種類を特定できないため、内容をそのまま記録する
                log.LogError($"フォントアセットの作成に失敗しました: {fontFilePath}\n{ex}");
                return null;
            }
        }

        /// <summary>
        /// 行の寸法(上端・下端・行の高さ)をゲーム標準フォントの比率へ揃えた FaceInfo を返す。
        /// 倍率を1より大きくした場合は、増えた分を上下へ均等に配る。
        /// </summary>
        private static FaceInfo AlignLineMetrics(FaceInfo face, float lineHeightScale)
        {
            var pointSize = (float)face.pointSize;
            var extra = (lineHeightScale - 1f) * BaseLineHeightRatio;

            face.ascentLine = (BaseAscentRatio + (extra / 2f)) * pointSize;
            face.descentLine = (BaseDescentRatio - (extra / 2f)) * pointSize;
            face.lineHeight = BaseLineHeightRatio * lineHeightScale * pointSize;
            return face;
        }
    }
}
