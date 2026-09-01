using System.Windows;
using System.Windows.Threading;

namespace DalView;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"예상치 못한 오류가 발생했습니다:\n{e.Exception.Message}", "달뷰", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
