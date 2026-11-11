using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace BioIncJp.Patcher;

/// <summary>
/// メイン画面。表示内容は <see cref="MainViewModel"/> が持ち、ここではボタンの操作を受けて結果を知らせるだけ。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    internal MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>中央のボタンが押されたとき。状態に応じて日本語化または復元を行い、結果を知らせる。</summary>
    private async void OnToggleClicked(object sender, RoutedEventArgs e)
    {
        // async void の中で起きた例外は呼び出し元へ伝わらないため、ここで受けて内容を表示する
        try
        {
            var result = await _viewModel.ToggleAsync();
            MessageBox.Show(
                this,
                result.Message,
                AppInfo.Title,
                MessageBoxButton.OK,
                result.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"予期しないエラーが発生しました。\n\n{ex.GetType().Name}: {ex.Message}",
                AppInfo.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>記録が追記されたら、最新の行が見えるよう末尾までスクロールする。</summary>
    private void OnLogChanged(object sender, TextChangedEventArgs e)
    {
        LogBox.ScrollToEnd();
    }

    /// <summary>
    /// ウィンドウを閉じようとしたとき。日本語化や復元の処理中は閉じさせない。
    /// 閉じるとアプリが終了し、ファイルの配置や削除が途中で打ち切られてしまうため。
    /// (途中で打ち切られても導入記録から元に戻せるが、利用者に余計な手間をかけさせない)
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            e.Cancel = true;
            MessageBox.Show(
                this,
                "処理の途中です。完了するまでお待ちください(途中で閉じると、ファイルの配置や削除が打ち切られます)。",
                AppInfo.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        base.OnClosing(e);
    }
}
