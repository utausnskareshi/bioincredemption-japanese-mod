using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BioIncJp.Plugin.Fonts;
using BioIncJp.Plugin.Patches;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace BioIncJp.Plugin
{
    /// <summary>
    /// プラグインの入口。BepInEx がゲーム起動時にこのクラスを読み込み、Awake を呼ぶ。
    /// ここでは設定の読み込みと、各機能のフックの取り付けだけを行う。
    /// </summary>
    [BepInPlugin(PluginIdentity.Guid, PluginIdentity.Name, BuildInfo.Version)]
    public sealed class JapanesePlugin : BaseUnityPlugin
    {
        /// <summary>
        /// 取り付けるフックの一覧。機能名はログに出すためのもので、
        /// ゲームの更新で一部のフックが取り付けられなくなったときに、どの機能が使えないかを利用者へ伝える。
        /// </summary>
        private static readonly IReadOnlyList<(Type PatchType, string Feature)> Patches = new[]
        {
            (typeof(LanguageLoadedPatch), "翻訳の適用"),
            (typeof(FontInitializedPatch), "日本語フォントの登録"),
            (typeof(CultureAppliedPatch), "日付・時刻の書式"),
            (typeof(DateFormatPatch), "ゲーム内日付の表記"),
            (typeof(SettingsPanelCreatedPatch), "設定画面の「日本語」ボタン"),
            (typeof(LanguageSelectionPatch), "言語ボタンの選択表示"),
            (typeof(BuiltInLanguageClickedPatch), "他言語への切り替え"),
            (typeof(ShowKeysPatch), "リソースキー表示(翻訳確認用)"),
            (typeof(KeyBindingRowPatch), "キー設定のキー名"),
            (typeof(KeyBindingRebindPatch), "キー設定のキー名(割り当て直したとき)"),
            (typeof(SandboxPrerequisitesPatch), "前提条件の病名の区切り"),
        };

        private PluginContext? _context;

        private void Awake()
        {
            var pluginDirectory = Path.GetDirectoryName(Info.Location);
            if (string.IsNullOrEmpty(pluginDirectory))
            {
                Logger.LogError("プラグインの配置場所を特定できないため、日本語化を開始できません。");
                return;
            }

            var context = new PluginContext(Logger, new PluginSettings(Config), pluginDirectory!);
            _context = context;

            HookGuard.Initialize(Logger);
            LocalizationService.Initialize(context);
            FontService.Initialize(context);
            JapaneseLanguageButton.Initialize(context);

            var failed = ApplyPatches(new Harmony(PluginIdentity.Guid), context);

            // シーンを読み込むたびに、新しく読み込まれたフォントへも日本語フォントを登録する
            SceneManager.sceneLoaded += OnSceneLoaded;

            var state = context.Settings.Enabled ? "有効" : "無効";
            Logger.LogInfo($"{PluginIdentity.Name} {BuildInfo.Version} を読み込みました (日本語表示: {state}, 取り付けに失敗したフック: {failed.Count} 件)");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            var context = _context;
            if (context is null)
            {
                return;
            }

            // 翻訳確認用のショートカット。通常のプレイでは使わない。
            if (context.Settings.ReloadShortcut.IsDown())
            {
                LocalizationService.ReloadFromDisk();
            }

            if (context.Settings.ToggleKeysShortcut.IsDown())
            {
                context.Settings.SetShowKeys(!context.Settings.ShowKeys);
                LocalizationService.ReloadFromDisk();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // シーン読み込みの通知はゲームの読み込み処理の中で呼ばれるので、フックと同じく例外を外へ出さない
            try
            {
                FontService.RegisterToAllLoadedFonts();
            }
            catch (Exception ex)
            {
                HookGuard.Report("日本語フォントの登録", ex);
            }
        }

        /// <summary>
        /// フックを1つずつ取り付ける。1つが失敗しても残りは取り付け、失敗した機能名を返す。
        /// (まとめて取り付けると、ゲームの更新で1か所でも対象が無くなったときに全機能が止まってしまう)
        /// </summary>
        private static IReadOnlyList<string> ApplyPatches(Harmony harmony, PluginContext context)
        {
            return Patches
                .Where(patch => !TryApplyPatch(harmony, context, patch.PatchType, patch.Feature))
                .Select(patch => patch.Feature)
                .ToArray();
        }

        private static bool TryApplyPatch(Harmony harmony, PluginContext context, Type patchType, string feature)
        {
            try
            {
                harmony.CreateClassProcessor(patchType).Patch();
                return true;
            }
            catch (Exception ex)
            {
                // Harmony は対象メソッドが見つからない場合などに様々な例外を投げるため、種類を限定せず受けて内容を記録する
                context.Log.LogError(
                    $"機能「{feature}」を有効にできませんでした。ゲームの更新で内部構造が変わった可能性があります。\n{ex}");
                return false;
            }
        }
    }
}
