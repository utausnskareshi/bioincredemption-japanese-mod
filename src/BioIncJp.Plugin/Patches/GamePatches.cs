using System;
using System.Linq;
using System.Xml;
using BioIncJp.Localization;
using BioIncJp.Plugin.Fonts;
using HarmonyLib;
using TMPro;
using dryginstudios.bioinc.healthconditions;
using dryginstudios.bioinc.sandbox;
using dryginstudios.bioinc.settings;
using dryginstudios.commonscripts.button;
using dryginstudios.commonscripts.input;
using dryginstudios.commonscripts.resourcemanager;
using dryginstudios.commonscripts.utils;

// このファイルには、ゲームのメソッドへ処理を差し込む「フック」だけをまとめている。
// 各フックは対象メソッドの前後で呼ばれる小さな橋渡しで、実際の処理は各サービスに任せる。
// フックはゲームのファイルを書き換えず、実行中のメモリ上でだけ動作を変える。
//
// どのフックも、中で起きた例外をゲーム本体へ伝えない(HookGuard で記録して続行する)。
// フックはゲームのメソッドの一部として動くので、例外が飛び出すとゲームの起動や描画が止まってしまうため。
// 捕まえる例外の種類を限定していないのは、プラグイン側のどんな不具合でもゲームを巻き添えにしないためである。
namespace BioIncJp.Plugin.Patches
{
    /// <summary>
    /// ゲームが言語XMLを辞書へ読み込んだ直後に、日本語訳を重ねる。
    /// 対象: ResourceManager.LoadCulture(XmlDocument, string)
    /// </summary>
    [HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.LoadCulture), new[] { typeof(XmlDocument), typeof(string) })]
    internal static class LanguageLoadedPatch
    {
        private static void Postfix()
        {
            try
            {
                LocalizationService.ApplyToGame();
            }
            catch (Exception ex)
            {
                HookGuard.Report("翻訳の適用", ex);
            }
        }
    }

    /// <summary>
    /// 翻訳確認用: 文言を取り出すときに、設定に応じてリソースキーを前に付ける。
    /// 対象: ResourceManager.GetValue(string)
    /// </summary>
    [HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.GetValue), new[] { typeof(string) })]
    internal static class ShowKeysPatch
    {
        private static void Postfix(string key, ref string __result)
        {
            try
            {
                __result = LocalizationService.DecorateIfDebug(key, __result);
            }
            catch (Exception ex)
            {
                HookGuard.Report("リソースキー表示(翻訳確認用)", ex);
            }
        }
    }

    /// <summary>
    /// ゲームが起動時に言語とカルチャを設定した直後に、日付などの書式を日本語へ切り替える。
    /// 対象: GameManager.LoadCurrentLanguage()
    /// </summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadCurrentLanguage))]
    internal static class CultureAppliedPatch
    {
        private static void Postfix()
        {
            try
            {
                LocalizationService.ApplyJapaneseCulture();
            }
            catch (Exception ex)
            {
                HookGuard.Report("日付・時刻の書式", ex);
            }
        }
    }

    /// <summary>
    /// ゲーム内の日付表示(「10 Nov 2026」形式)を「2026年11月10日」へ置き換える。
    /// 対象: Utils.FormatDate(DateTime)
    /// </summary>
    [HarmonyPatch(typeof(Utils), nameof(Utils.FormatDate), new[] { typeof(DateTime) })]
    internal static class DateFormatPatch
    {
        private static void Postfix(DateTime date, ref string __result)
        {
            try
            {
                if (LocalizationService.IsJapaneseActive)
                {
                    __result = JapaneseFormat.FormatDate(date);
                }
            }
            catch (Exception ex)
            {
                HookGuard.Report("ゲーム内日付の表記", ex);
            }
        }
    }

    /// <summary>
    /// TextMeshPro がフォントを初期化した直後に、日本語フォントをフォールバックへ登録する。
    /// 対象: TMP_FontAsset.ReadFontAssetDefinition()
    /// </summary>
    [HarmonyPatch(typeof(TMP_FontAsset), nameof(TMP_FontAsset.ReadFontAssetDefinition))]
    internal static class FontInitializedPatch
    {
        private static void Postfix(TMP_FontAsset __instance)
        {
            try
            {
                FontService.OnFontAssetInitialized(__instance);
            }
            catch (Exception ex)
            {
                HookGuard.Report("日本語フォントの登録", ex);
            }
        }
    }

    /// <summary>
    /// 設定画面が生成された直後に「日本語」ボタンを追加する。
    /// 対象: SettingsPanel.Awake()
    /// </summary>
    [HarmonyPatch(typeof(SettingsPanel), "Awake")]
    internal static class SettingsPanelCreatedPatch
    {
        private static void Postfix(SettingsPanel __instance)
        {
            try
            {
                JapaneseLanguageButton.Install(__instance);
            }
            catch (Exception ex)
            {
                HookGuard.Report("設定画面の「日本語」ボタン", ex);
            }
        }
    }

    /// <summary>
    /// ゲームが言語ボタンの選択表示を更新した直後に、日本語ボタンの選択表示を合わせる。
    /// 対象: SettingsPanel.SelectCurrentLanguage()
    /// </summary>
    [HarmonyPatch(typeof(SettingsPanel), "SelectCurrentLanguage")]
    internal static class LanguageSelectionPatch
    {
        private static void Postfix(SettingsPanel __instance)
        {
            try
            {
                JapaneseLanguageButton.RefreshSelection(__instance);
            }
            catch (Exception ex)
            {
                HookGuard.Report("言語ボタンの選択表示", ex);
            }
        }
    }

    /// <summary>
    /// 元からある言語ボタンが押されたとき、その処理(選択表示の更新を含む)より前に日本語表示を無効にする。
    /// 対象: SettingsPanel.LanguageClicked(GenericButton)
    /// </summary>
    [HarmonyPatch(typeof(SettingsPanel), "LanguageClicked", new[] { typeof(GenericButton) })]
    internal static class BuiltInLanguageClickedPatch
    {
        private static void Prefix()
        {
            try
            {
                JapaneseLanguageButton.OnBuiltInLanguageClicked();
            }
            catch (Exception ex)
            {
                HookGuard.Report("他言語への切り替え", ex);
            }
        }
    }

    /// <summary>
    /// キー設定画面の一覧で、キーの名前を日本語向けの表記にする(ゲームは Unity の内部名「Alpha1」などをそのまま表示する)。
    /// ゲームはキーの割り当てを内部の値で保持し、ボタンの文字を読み戻さないので、表示だけを差し替えても動作は変わらない。
    /// 対象: KeyBindingPanelRow.SetKeyForRow(DGInput)
    /// </summary>
    [HarmonyPatch(typeof(KeyBindingPanelRow), nameof(KeyBindingPanelRow.SetKeyForRow))]
    internal static class KeyBindingRowPatch
    {
        private static void Postfix(KeyBindingPanelRow __instance, DGInput key)
        {
            try
            {
                if (!LocalizationService.IsJapaneseActive || __instance == null || key == null)
                {
                    return;
                }

                __instance.PrimaryKey.ButtonText.text = JapaneseFormat.DescribeKey(key.PrimaryKey.ToString());
                __instance.SecondaryKey.ButtonText.text = JapaneseFormat.DescribeKey(key.SecondaryKey.ToString());
            }
            catch (Exception ex)
            {
                HookGuard.Report("キー設定のキー名", ex);
            }
        }
    }

    /// <summary>
    /// キーを割り当て直したときに表示するキーの名前も、日本語向けの表記にする。
    /// 対象: KeyBindingPanel.GetTextFromKey(KeyCode)(非公開)
    /// </summary>
    [HarmonyPatch(typeof(KeyBindingPanel), "GetTextFromKey")]
    internal static class KeyBindingRebindPatch
    {
        private static void Postfix(ref string __result)
        {
            try
            {
                if (LocalizationService.IsJapaneseActive && __result != null)
                {
                    __result = JapaneseFormat.DescribeKey(__result);
                }
            }
            catch (Exception ex)
            {
                HookGuard.Report("キー設定のキー名", ex);
            }
        }
    }

    /// <summary>
    /// サンドボックスの病気の一覧で、前提条件の病名の区切りを「、」にする(ゲームは半角のコンマでつなぐ)。
    /// 対象: SandboxHealthConditionListItem.Condition の設定(set_Condition)
    /// </summary>
    [HarmonyPatch(typeof(SandboxHealthConditionListItem), nameof(SandboxHealthConditionListItem.Condition), MethodType.Setter)]
    internal static class SandboxPrerequisitesPatch
    {
        private static void Postfix(SandboxHealthConditionListItem __instance, HealthConditionBase value)
        {
            try
            {
                if (!LocalizationService.IsJapaneseActive || __instance == null || __instance.RequiredText == null || value?.Prerequisites == null)
                {
                    return;
                }

                __instance.RequiredText.text = JapaneseFormat.JoinNames(value.Prerequisites.Select(prerequisite => prerequisite.DisplayName));
            }
            catch (Exception ex)
            {
                HookGuard.Report("前提条件の病名の区切り", ex);
            }
        }
    }
}
