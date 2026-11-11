using System;
using BepInEx.Configuration;

namespace BioIncJp.Plugin
{
    /// <summary>
    /// プラグインの設定値。BepInEx の設定ファイル(BepInEx\config\&lt;GUID&gt;.cfg)と1対1に対応する。
    /// 設定の読み書きはこのクラスに集約し、他のコードは型の付いたプロパティだけを見る。
    /// </summary>
    internal sealed class PluginSettings
    {
        private readonly ConfigEntry<bool> _enabled;
        private readonly ConfigEntry<string> _translationFile;
        private readonly ConfigEntry<string> _regularFontFile;
        private readonly ConfigEntry<string> _boldFontFile;
        private readonly ConfigEntry<int> _samplingPointSize;
        private readonly ConfigEntry<int> _atlasPadding;
        private readonly ConfigEntry<int> _atlasSize;
        private readonly ConfigEntry<bool> _prewarmGlyphs;
        private readonly ConfigEntry<float> _lineHeightScale;
        private readonly ConfigEntry<bool> _showKeys;
        private readonly ConfigEntry<KeyboardShortcut> _reloadShortcut;
        private readonly ConfigEntry<KeyboardShortcut> _toggleKeysShortcut;

        public PluginSettings(ConfigFile config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _enabled = config.Bind(
                "General", "Enabled", true,
                "日本語表示を有効にするか。ゲーム内の設定画面で「日本語」や他の言語を選ぶと自動で切り替わります(反映はゲームの再起動後)。");

            _translationFile = config.Bind(
                "General", "TranslationFile", "ja.xml",
                "翻訳ファイルの名前(プラグインのフォルダからの相対パス)。");

            _regularFontFile = config.Bind(
                "Font", "RegularFontFile", "fonts/NotoSansJP-Medium.otf",
                "通常の太さの文字に使うフォントファイル(TTF/OTF。プラグインのフォルダからの相対パス)。");

            _boldFontFile = config.Bind(
                "Font", "BoldFontFile", "fonts/NotoSansJP-Bold.otf",
                "太字の文字に使うフォントファイル(TTF/OTF。プラグインのフォルダからの相対パス)。");

            _samplingPointSize = config.Bind(
                "Font", "SamplingPointSize", 64,
                new ConfigDescription(
                    "フォントを画像化するときの文字の大きさ(ピクセル)。大きいほど拡大時に綺麗ですが、メモリを多く使います。",
                    new AcceptableValueRange<int>(24, 128)));

            _atlasPadding = config.Bind(
                "Font", "AtlasPadding", 10,
                new ConfigDescription(
                    "文字の周囲に確保する余白(ピクセル)。縁取りや光彩の太さに影響します。",
                    new AcceptableValueRange<int>(2, 24)));

            _atlasSize = config.Bind(
                "Font", "AtlasSize", 4096,
                new ConfigDescription(
                    "文字を並べる画像1枚の一辺の大きさ(ピクセル)。足りなくなると自動で2枚目が作られます。",
                    new AcceptableValueList<int>(1024, 2048, 4096)));

            _prewarmGlyphs = config.Bind(
                "Font", "PrewarmGlyphs", true,
                "起動時に翻訳で使う文字をまとめて画像化しておくか。無効にすると、初めて表示される文字のたびに少しずつ生成します。");

            _lineHeightScale = config.Bind(
                "Font", "LineHeightScale", 1.0f,
                new ConfigDescription(
                    "日本語フォントの行の高さを、ゲーム標準フォントの行の高さに対して何倍にするか。",
                    new AcceptableValueRange<float>(0.8f, 2.0f)));

            _showKeys = config.Bind(
                "Debug", "ShowKeys", false,
                "翻訳確認用。各文言の前にリソースキーを表示します。");

            _reloadShortcut = config.Bind(
                "Debug", "ReloadShortcut", new KeyboardShortcut(UnityEngine.KeyCode.F10, UnityEngine.KeyCode.LeftControl),
                "翻訳確認用。翻訳ファイルを読み込み直すキー。");

            _toggleKeysShortcut = config.Bind(
                "Debug", "ToggleKeysShortcut", new KeyboardShortcut(UnityEngine.KeyCode.F9, UnityEngine.KeyCode.LeftControl),
                "翻訳確認用。リソースキー表示を切り替えるキー。");
        }

        /// <summary>日本語表示が有効か。</summary>
        public bool Enabled => _enabled.Value;

        /// <summary>翻訳ファイルの相対パス。</summary>
        public string TranslationFile => _translationFile.Value;

        /// <summary>通常フォントの相対パス。</summary>
        public string RegularFontFile => _regularFontFile.Value;

        /// <summary>太字フォントの相対パス。</summary>
        public string BoldFontFile => _boldFontFile.Value;

        /// <summary>フォント画像化時の文字サイズ。</summary>
        public int SamplingPointSize => _samplingPointSize.Value;

        /// <summary>文字周囲の余白。</summary>
        public int AtlasPadding => _atlasPadding.Value;

        /// <summary>文字画像の一辺。</summary>
        public int AtlasSize => _atlasSize.Value;

        /// <summary>起動時に文字をまとめて生成するか。</summary>
        public bool PrewarmGlyphs => _prewarmGlyphs.Value;

        /// <summary>行の高さの倍率。</summary>
        public float LineHeightScale => _lineHeightScale.Value;

        /// <summary>リソースキーを表示するか。</summary>
        public bool ShowKeys => _showKeys.Value;

        /// <summary>翻訳の再読み込みキー。</summary>
        public KeyboardShortcut ReloadShortcut => _reloadShortcut.Value;

        /// <summary>キー表示の切り替えキー。</summary>
        public KeyboardShortcut ToggleKeysShortcut => _toggleKeysShortcut.Value;

        /// <summary>日本語表示の有効/無効を変更し、設定ファイルへ保存する。</summary>
        public void SetEnabled(bool enabled)
        {
            _enabled.Value = enabled;
        }

        /// <summary>リソースキー表示の有無を変更し、設定ファイルへ保存する。</summary>
        public void SetShowKeys(bool showKeys)
        {
            _showKeys.Value = showKeys;
        }
    }
}
