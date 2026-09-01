using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DalView;

public partial class App : Application
{
    private const string MutexName = "DalView_SingleInstance_Mutex";
    private const string PipeName = "DalView_OpenFile_Pipe";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var filePath = e.Args.Length > 0 ? e.Args[0] : null;

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            if (filePath != null)
            {
                SendFileToRunningInstance(filePath);
            }
            Shutdown();
            return;
        }

        StartPipeServer();

        _mainWindow = new MainWindow();
        _mainWindow.Show();

        if (filePath != null)
        {
            _mainWindow.OpenFileFromExternalRequest(filePath);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Only release the mutex if this instance actually owns it. When a second instance's
        // Mutex(initiallyOwned: true, ...) call finds the mutex already held (createdNew == false),
        // ownership is NOT granted to this thread — calling ReleaseMutex() in that case throws
        // SynchronizationLockException instead of exiting cleanly, leaving a stray process.
        if (_ownsMutex)
        {
            _mutex?.ReleaseMutex();
        }
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"예상치 못한 오류가 발생했습니다:\n{e.Exception.Message}", "달뷰", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void StartPipeServer()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var path = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(path))
                    {
                        Dispatcher.BeginInvoke(() => _mainWindow?.OpenFileFromExternalRequest(path));
                    }
                }
                catch
                {
                    // Pipe faulted or was torn down mid-connection; loop and open a fresh one.
                    await Task.Delay(500);
                }
            }
        });
    }

    private static void SendFileToRunningInstance(string filePath)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(filePath);
        }
        catch
        {
            // Best-effort handoff: if the running instance can't be reached, give up quietly
            // rather than surfacing an error for what the user experiences as "opening a file".
        }
    }
}
