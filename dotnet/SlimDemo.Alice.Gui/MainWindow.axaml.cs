using System.Collections.ObjectModel;
using Agntcy.Slim;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SlimDemo.Alice.Common;

namespace SlimDemo.Alice.Gui;

public partial class MainWindow : Window
{

    readonly ObservableCollection<string> _messages = new();
    SlimApp? _app;
    CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        MessageList.ItemsSource = _messages;
    }

    void Log(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _messages.Add($"[{DateTime.Now:HH:mm:ss}] {text}");
            LogScroller.ScrollToEnd();
        });
    }

    void SetStatus(string text, IBrush color)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = text;
            StatusDot.Fill = color;
        });
    }

    void SetButtons(bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConnectBtn.IsEnabled = !connected;
            DisconnectBtn.IsEnabled = connected;
        });
    }

    void OnConnect(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetButtons(true);
        SetStatus("Connecting...", Brushes.Orange);

        Task.Run(() => RunReceiver(ct), ct);
    }

    void OnDisconnect(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cts?.Cancel();
        _app?.Destroy();
        _app = null;
        SetButtons(false);
        SetStatus("Disconnected", Brushes.Gray);
        Log("Disconnected.");
    }

    async Task RunReceiver(CancellationToken ct)
    {
        try
        {
            var (app, connId) = AliceReceiver.CreateAndConnect(DemoConfig.DefaultServer, DemoConfig.DefaultSecret);
            _app = app;

            SetStatus($"Listening (conn {connId})", Brushes.LimeGreen);
            Log($"Connected to {DemoConfig.DefaultServer} — waiting for sessions...");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var session = await app.ListenForSessionAsync();
                    Log("New session established!");
                    _ = Task.Run(() => SessionHandler.RunAsync(session, msg => Log(msg), ct), ct);
                }
                catch when (ct.IsCancellationRequested) { break; }
                catch { /* timeout, retry */ }
            }
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            SetStatus("Error", Brushes.Red);
            SetButtons(false);
        }
    }

}
