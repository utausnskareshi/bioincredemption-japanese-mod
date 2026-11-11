using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BioIncJp.Localization;
using TMPro;
using UnityEngine;

namespace BioIncJp.Plugin.Fonts
{
    /// <summary>
    /// 日本語フォントを作り、ゲームの各フォントのフォールバックへ登録する処理の窓口。
    ///
    /// ゲームの文字はほぼすべて TextMeshPro で描かれ、主に使われる Exo 2 フォントには日本語の文字が無い。
    /// TextMeshPro は持っていない文字をフォールバックの一覧から順に探すので、そこへ日本語フォントを加える。
    /// </summary>
    internal static class FontService
    {
        private static PluginContext? _context;

        // 作成済みの日本語フォント。太字の作成に失敗した場合は、太字にも通常の太さを使う。
        private static TMP_FontAsset? _regular;
        private static TMP_FontAsset? _bold;

        // 作成は1度だけ試みる(失敗するたびに毎回やり直すと、文字を描くたびにファイルを読みに行ってしまう)
        private static bool _creationAttempted;

        // 自分でフォントを作っている最中かどうか。作成処理の中からも「フォント初期化」のフックが呼ばれるため、再入を防ぐ。
        private static bool _creating;

        /// <summary>起動時に1度だけ呼ぶ。</summary>
        public static void Initialize(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// TextMeshPro がフォントアセットを初期化した直後に呼ばれる。
        /// フォントは最初に文字を探す前に必ず初期化されるので、ここで登録すれば
        /// 「先に中国語フォントで文字が見つかってしまい、それが使い回される」ことを防げる。
        /// </summary>
        public static void OnFontAssetInitialized(TMP_FontAsset asset)
        {
            if (_creating || asset == null)
            {
                return;
            }

            EnsureCreated();
            RegisterTo(asset);
        }

        /// <summary>
        /// 現在メモリ上にあるすべてのフォントアセットと、TextMeshPro 全体のフォールバックへ登録する。
        /// シーン読み込みのたびに呼び、取りこぼしを無くす。
        /// </summary>
        public static void RegisterToAllLoadedFonts()
        {
            if (_creating)
            {
                return;
            }

            EnsureCreated();
            var regular = _regular;
            var context = _context;
            if (regular == null || context is null)
            {
                return;
            }

            foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                RegisterTo(asset);
            }

            // どのフォントのフォールバックにも無かった文字が、最後に探しに来る全体の一覧
            var globalFallbacks = TMP_Settings.fallbackFontAssets;
            if (globalFallbacks != null)
            {
                FontFallbacks.Ensure(globalFallbacks, regular, LocalizationService.IsJapaneseActive);
            }
        }

        /// <summary>1つのフォントアセットのフォールバックへ日本語フォントを登録する。</summary>
        private static void RegisterTo(TMP_FontAsset asset)
        {
            var regular = _regular;
            var context = _context;
            if (regular == null || context is null || asset == null)
            {
                return;
            }

            // 日本語フォント自身には登録しない(自分を自分のフォールバックにする意味が無い)
            if (ReferenceEquals(asset, _regular) || ReferenceEquals(asset, _bold))
            {
                return;
            }

            var japanese = FontFallbacks.UsesBoldWeight(asset.name) && _bold != null ? _bold : regular;
            if (asset.fallbackFontAssetTable == null)
            {
                asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }

            FontFallbacks.Ensure(asset.fallbackFontAssetTable, japanese, LocalizationService.IsJapaneseActive);
        }

        /// <summary>日本語フォントをまだ作っていなければ作る。</summary>
        private static void EnsureCreated()
        {
            var context = _context;
            if (_creationAttempted || context is null)
            {
                return;
            }

            _creationAttempted = true;
            _creating = true;
            try
            {
                var settings = context.Settings;
                var options = new FontCreationOptions(
                    settings.SamplingPointSize, settings.AtlasPadding, settings.AtlasSize, settings.LineHeightScale);

                _regular = CreateFont(context, "RegularFontFile", settings.RegularFontFile, "BioIncJP Regular SDF", options);
                _bold = CreateFont(context, "BoldFontFile", settings.BoldFontFile, "BioIncJP Bold SDF", options);

                if (_regular == null)
                {
                    context.Log.LogError("日本語フォントを用意できなかったため、日本語の文字が正しく表示されない可能性があります。");
                    return;
                }

                context.Log.LogInfo(
                    $"日本語フォントを作成しました (通常: {settings.RegularFontFile}, 太字: {(_bold != null ? settings.BoldFontFile : "作成失敗のため通常で代用")})");

                if (LocalizationService.IsJapaneseActive && settings.PrewarmGlyphs)
                {
                    Prewarm(context, _regular);
                }
            }
            finally
            {
                _creating = false;
            }
        }

        /// <summary>
        /// 設定に書かれたフォントファイルからフォントを作る。パスが不正な場合やフォントを作れない場合は、原因を記録して null を返す。
        /// </summary>
        private static TMP_FontAsset? CreateFont(
            PluginContext context, string settingName, string relativePath, string assetName, FontCreationOptions options)
        {
            var path = context.TryResolvePath(relativePath, out var pathError);
            if (path is null)
            {
                context.Log.LogError($"設定 {settingName} が不正なため、フォントを読み込めません。{pathError}");
                return null;
            }

            return JapaneseFontFactory.Create(path, assetName, options, context.Log);
        }

        /// <summary>
        /// 翻訳で使われている文字を、あらかじめまとめて画像化しておく。
        /// これをしないと、新しい画面を開くたびに初出の文字を生成することになり、一瞬の引っかかりが出やすい。
        /// </summary>
        private static void Prewarm(PluginContext context, TMP_FontAsset font)
        {
            // ASCII はゲーム標準フォントが持っているので、日本語フォント側で用意する必要は無い
            var characters = new string(
                TranslationXml.CollectCharacters(LocalizationService.Catalog).Where(c => c > 0x7F && !char.IsControl(c)).ToArray());
            if (characters.Length == 0)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var allAdded = font.TryAddCharacters(characters, out var missing);
            stopwatch.Stop();

            context.Log.LogInfo($"文字を事前生成しました: {characters.Length} 種類, {stopwatch.ElapsedMilliseconds} ms");
            if (!allAdded && !string.IsNullOrEmpty(missing))
            {
                // フォントに収録されていない文字。別のフォールバック(中国語フォント等)で表示されるか、□になる。
                context.Log.LogWarning($"フォントに収録されていない文字があります ({missing.Length} 種類): {missing}");
            }
        }
    }
}
