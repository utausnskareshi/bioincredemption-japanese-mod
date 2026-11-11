using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using dryginstudios.bioinc.settings;
using dryginstudios.commonscripts.button;

namespace BioIncJp.Plugin
{
    /// <summary>
    /// ゲームの設定画面の言語一覧へ「日本語」ボタンを追加する。
    ///
    /// 既存の言語ボタンを複製して作るので、見た目や操作感は他の言語と同じになる。
    /// 日本語の有効/無効はプラグイン側の設定に保存し、ゲームのセーブデータには書き込まない
    /// (パッチを外したあとにセーブデータへ未知の言語コードが残らないようにするため)。
    /// 他の言語と同じく、切り替えはゲームの再起動後に反映される。
    /// </summary>
    internal static class JapaneseLanguageButton
    {
        /// <summary>ボタンに表示する文字。</summary>
        private const string Label = "日本語";

        private static PluginContext? _context;

        // 追加したボタン。設定画面はゲーム起動中ずっと同じものが使い回されるので、1つだけ保持する。
        private static GenericButton? _button;

        /// <summary>起動時に1度だけ呼ぶ。</summary>
        public static void Initialize(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>設定画面が生成された直後に呼ばれ、日本語ボタンを追加する。</summary>
        public static void Install(SettingsPanel panel)
        {
            var context = _context;
            if (context is null || panel == null)
            {
                return;
            }

            // 設定画面は各シーンに置かれているが、生き残るのは最初の1つだけ(残りは自分で破棄する)。破棄される方には付けない。
            if (SettingsPanel.Instance != panel || _button != null)
            {
                return;
            }

            // 複製元。行の末尾にある中国語ボタンの右隣へ追加する。
            var template = panel.ChineseButton;
            if (template == null)
            {
                context.Log.LogWarning("設定画面の言語ボタンが見つからないため、「日本語」ボタンを追加できませんでした。日本語表示自体には影響しません。");
                return;
            }

            var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = "Japanese";
            clone.transform.SetAsLastSibling();

            var button = clone.GetComponent<GenericButton>();
            if (button == null)
            {
                context.Log.LogWarning("複製した言語ボタンの構成が想定と異なるため、「日本語」ボタンを追加できませんでした。");
                UnityEngine.Object.Destroy(clone);
                return;
            }

            var text = button.ButtonText != null ? button.ButtonText : clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = Label;
            }

            button.OnClick.AddListener(OnJapaneseClicked);
            ConnectNavigation(template.GetButton, button.GetButton);

            _button = button;
            context.Log.LogInfo("設定画面に「日本語」ボタンを追加しました。");
        }

        /// <summary>
        /// ゲームが言語ボタンの選択状態を更新した直後に呼ばれる。
        /// 日本語が有効なら、日本語ボタンだけを選択状態にする。
        /// </summary>
        public static void RefreshSelection(SettingsPanel panel)
        {
            var context = _context;
            var button = _button;
            if (context is null || button == null || panel == null)
            {
                return;
            }

            var japaneseSelected = context.Settings.Enabled;
            if (japaneseSelected)
            {
                foreach (var builtIn in BuiltInLanguageButtons(panel))
                {
                    builtIn.Selected(false);
                }
            }

            button.Selected(japaneseSelected);
        }

        /// <summary>
        /// 元からある言語ボタン(英語など)が押される直前に呼ばれる。
        /// 利用者がその言語を選んだということなので、日本語表示を無効にする。
        /// </summary>
        public static void OnBuiltInLanguageClicked()
        {
            var context = _context;
            if (context is null || !context.Settings.Enabled)
            {
                return;
            }

            context.Settings.SetEnabled(false);
            context.Log.LogInfo("ゲーム標準の言語が選ばれたため、日本語表示を無効にしました(再起動後に反映されます)。");
        }

        /// <summary>日本語ボタンが押されたときの処理。</summary>
        private static void OnJapaneseClicked(GenericButton button)
        {
            var context = _context;
            if (context is null)
            {
                return;
            }

            if (!context.Settings.Enabled)
            {
                context.Settings.SetEnabled(true);
                context.Log.LogInfo("日本語表示を有効にしました(再起動後に反映されます)。");
            }

            if (SettingsPanel.Instance != null)
            {
                RefreshSelection(SettingsPanel.Instance);
            }
        }

        /// <summary>元からある言語ボタンの一覧(ゲームの更新で一部が無くなっていても動くよう、無いものは除く)。</summary>
        private static IEnumerable<GenericButton> BuiltInLanguageButtons(SettingsPanel panel)
        {
            return new[]
            {
                panel.FrenchButton, panel.EnglishButton, panel.RussianButton, panel.GermanButton, panel.EspanolButton,
                panel.PortuguesButton, panel.PolishButton, panel.SlovakButton, panel.ChineseButton, panel.TurkishButton,
            }.Where(b => b != null);
        }

        /// <summary>
        /// ゲームパッドでの移動先をつなぐ。複製元の右に日本語ボタンを挟み込む形にする。
        /// </summary>
        private static void ConnectNavigation(Button? template, Button? added)
        {
            if (template == null || added == null)
            {
                return;
            }

            var templateNavigation = template.navigation;
            var addedNavigation = templateNavigation;
            addedNavigation.mode = Navigation.Mode.Explicit;
            addedNavigation.selectOnLeft = template;
            addedNavigation.selectOnRight = templateNavigation.selectOnRight;
            added.navigation = addedNavigation;

            templateNavigation.selectOnRight = added;
            template.navigation = templateNavigation;
        }
    }
}
