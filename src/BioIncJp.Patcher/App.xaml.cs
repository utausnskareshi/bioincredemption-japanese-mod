using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using BioIncJp.Patcher.Core;
using BioIncJp.Patcher.Core.Steam;

namespace BioIncJp.Patcher;

/// <summary>
/// アプリケーションの入口。
/// 起動時にゲームがインストールされているかを調べ、無ければその旨を表示して終了する。
/// </summary>
public partial class App : Application
{
    // 親プロセスのコンソールを指す定数(AttachConsole 用)
    private const int AttachParentProcess = -1;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = CommandLineOptions.Parse(e.Args);
        if (options.Error is not null)
        {
            TryAttachParentConsole();
            Console.Error.WriteLine(options.Error);
            Console.Error.WriteLine(CommandLineOptions.Usage);
            Shutdown(HeadlessRunner.ExitInvalidArguments);
            return;
        }

        // 画面を出さない実行(--status / --apply / --revert)
        if (options.Action != HeadlessAction.None)
        {
            TryAttachParentConsole();
            Shutdown(HeadlessRunner.Run(options));
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;

        var detection = options.GameDirectory is null
            ? GameLocator.Locate(new RegistrySteamRootProvider())
            : GameLocator.FromDirectory(options.GameDirectory);

        if (detection is not GameDetectionResult.Found found)
        {
            // ゲームが無い場合は、その旨を伝えて終了する
            var message = ((GameDetectionResult.NotFound)detection).Message;
            MessageBox.Show(message, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(HeadlessRunner.ExitGameNotFound);
            return;
        }

        var service = new PatchService(options.PayloadDirectory ?? AppInfo.DefaultPayloadDirectory, AppInfo.Version);
        var window = new MainWindow(new MainViewModel(found.Game, service));
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    /// <summary>
    /// 想定していない例外が画面のスレッドで起きた場合の最後の受け皿。
    /// 黙って落ちるのではなく、内容を表示してから終了する。
    /// </summary>
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"予期しないエラーが発生したため、終了します。\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
            AppInfo.Title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(HeadlessRunner.ExitFailed);
    }

    /// <summary>
    /// コマンドプロンプトなどから起動された場合に、その画面へ出力できるようにする。
    /// このアプリは画面つきのアプリとして作られているため、何もしないと標準出力がどこにも表示されない。
    /// </summary>
    private static void TryAttachParentConsole()
    {
        if (!AttachConsole(AttachParentProcess))
        {
            // 親がコンソールを持たない場合(エクスプローラーからの起動など)。出力先が無いだけなので、そのまま続ける。
            return;
        }

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (System.IO.IOException)
        {
            // 文字コードを変更できないコンソールでも、出力自体はできるので続ける
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);
}
