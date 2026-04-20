// SysManager — Windows system monitoring toolkit
// Author : laurentiu021 · https://github.com/laurentiu021/SysManager
// License: MIT

using System.Windows;
using System.Windows.Threading;
using SysManager.Services;

namespace SysManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LogService.Init();
        DispatcherUnhandledException += OnUi;
        AppDomain.CurrentDomain.UnhandledException += OnDomain;
        TaskScheduler.UnobservedTaskException += OnTask;
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Shutdown();
        base.OnExit(e);
    }

    private static void OnUi(object s, DispatcherUnhandledExceptionEventArgs e)
    {
        LogService.Logger?.Error(e.Exception, "UI thread exception");
        MessageBox.Show(e.Exception.Message, "SysManager error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomain(object s, UnhandledExceptionEventArgs e)
        => LogService.Logger?.Error(e.ExceptionObject as Exception, "Domain exception");

    private static void OnTask(object? s, UnobservedTaskExceptionEventArgs e)
    {
        LogService.Logger?.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
