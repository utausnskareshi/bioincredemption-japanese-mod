using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BioIncJp.Patcher.Core;
using BioIncJp.Patcher.Core.Steam;

namespace BioIncJp.Patcher;

/// <summary>
/// メイン画面の表示内容と操作。
/// 画面(XAML)はこのクラスのプロパティを表示するだけで、判断や処理はここと中核ライブラリが行う。
/// </summary>
internal sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly GameInstallation _game;
    private readonly PatchService _service;

    private PatchScreenState _state;
    private bool _isBusy;
    private string _logText = string.Empty;

    public MainViewModel(GameInstallation game, PatchService service)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _state = PatchScreenState.From(game, PatchService.GetStatus(game));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>画面のタイトル。</summary>
    public string Title => AppInfo.Title;

    /// <summary>ツールの版の表示。</summary>
    public string VersionText => "v" + AppInfo.Version;

    /// <summary>ゲームのフォルダ。</summary>
    public string GamePath => _game.RootPath;

    /// <summary>現在の状態の説明。</summary>
    public string StatusText => _state.StatusText;

    /// <summary>日本語化済みか(状態の色分けに使う)。</summary>
    public bool IsPatched => _state.IsPatched;

    /// <summary>中央のボタンの文字。</summary>
    public string ButtonText => _state.ButtonText;

    /// <summary>補足の説明(複数ある場合は改行でつなぐ)。</summary>
    public string NotesText => string.Join("\n", _state.Notes);

    /// <summary>補足の説明があるか。</summary>
    public bool HasNotes => _state.Notes.Count > 0;

    /// <summary>処理の記録(画面下部に表示する)。</summary>
    public string LogText => _logText;

    /// <summary>処理中か。</summary>
    public bool IsBusy => _isBusy;

    /// <summary>ボタンを押せるか。</summary>
    public bool CanOperate => !_isBusy;

    /// <summary>
    /// 現在の状態に応じて「日本語化」か「元に戻す」を実行する。
    /// ファイル操作で画面が固まらないよう、処理は別スレッドで行う。
    /// </summary>
    public async Task<OperationResult> ToggleAsync()
    {
        var revert = _state.IsPatched;
        SetBusy(true);
        AppendLog(revert ? "元に戻しています…" : "日本語化しています…");

        try
        {
            // Progress は作られたスレッド(画面のスレッド)で報告を受け取るので、ここから記録へ追記できる
            var progress = new Progress<string>(AppendLog);
            var result = await Task.Run(() => revert ? _service.Revert(_game, progress) : _service.Apply(_game, progress));

            AppendLog(result.Message);
            return result;
        }
        finally
        {
            _state = PatchScreenState.From(_game, PatchService.GetStatus(_game));
            SetBusy(false);
            NotifyStateChanged();
        }
    }

    /// <summary>処理の記録へ1行追記する。</summary>
    private void AppendLog(string message)
    {
        _logText = _logText.Length == 0 ? message : _logText + Environment.NewLine + message;
        OnPropertyChanged(nameof(LogText));
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanOperate));
    }

    /// <summary>状態に由来する表示項目がすべて変わったことを画面へ知らせる。</summary>
    private void NotifyStateChanged()
    {
        foreach (var name in new[] { nameof(StatusText), nameof(IsPatched), nameof(ButtonText), nameof(NotesText), nameof(HasNotes) })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
