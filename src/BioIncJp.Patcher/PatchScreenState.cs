using System;
using System.Collections.Generic;
using System.Linq;
using BioIncJp.Patcher.Core;
using BioIncJp.Patcher.Core.Steam;

namespace BioIncJp.Patcher;

/// <summary>
/// 画面に表示する内容(不変)。ゲームの情報とパッチの状態から決まる。
/// </summary>
/// <param name="StatusText">現在の状態の説明。</param>
/// <param name="ButtonText">中央のボタンの文字。</param>
/// <param name="IsPatched">日本語化済みか。ボタンを押したときの操作(日本語化 / 元に戻す)を決める。</param>
/// <param name="Notes">補足の説明(未確認のビルドである等)。</param>
internal sealed record PatchScreenState(string StatusText, string ButtonText, bool IsPatched, IReadOnlyList<string> Notes)
{
    /// <summary>ゲームの情報とパッチの状態から、表示内容を作る(副作用なし)。</summary>
    public static PatchScreenState From(GameInstallation game, PatchStatus status)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(status);

        var statusText = status.State switch
        {
            PatchState.NotPatched => "日本語化されていません",
            PatchState.Patched => status.Manifest is null
                ? "日本語化済み"
                : $"日本語化済み(パッチ v{status.Manifest.ToolVersion})",
            PatchState.PatchedWithoutRecord => "日本語化済み(このツールの導入記録なし)",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status.State, "未対応の状態です。"),
        };

        var notes = new[]
        {
            status.Note,
            game.IsFullyInstalled ? null : "Steam でゲームの更新または検証が進行中の可能性があります。完了してから操作することをおすすめします。",
            // ビルド番号が分かっていて、かつ未確認の場合だけ知らせる
            game.BuildId.Length > 0 && !AppInfo.IsVerifiedBuild(game.BuildId)
                ? $"このゲームのバージョン(ビルド {game.BuildId})での動作は未確認です。日本語化はできますが、一部が英語のまま表示される可能性があります。"
                : null,
        };

        return new PatchScreenState(
            statusText,
            status.IsPatched ? "元に戻す" : "日本語化する",
            status.IsPatched,
            notes.Where(note => note is not null).Select(note => note!).ToArray());
    }
}
