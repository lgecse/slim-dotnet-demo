using System.Collections.ObjectModel;
using Agntcy.Slim;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SlimDemo.Common;

namespace SlimDemo.Group.Gui;

public partial class MainWindow : Window
{
    readonly ObservableCollection<string> _messages = new();

    SlimApp? _app;
    SlimSession? _session;
    ulong _connId;
    bool _hasConnection;
    CancellationTokenSource? _cts;
    HashSet<string> _knownParticipants = new();
    string _identity = "";

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

    void UpdateParticipantsDisplay(IReadOnlyList<SlimName>? participants = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (participants == null || participants.Count == 0)
            {
                ParticipantsText.Text = "(none)";
                return;
            }
            ParticipantsText.Text = string.Join(", ", participants.Select(p => p.ToString()));
        });
    }

    bool _isConnected;

    void SetConnectedState(bool listening = false)
    {
        _isConnected = true;
        Dispatcher.UIThread.Post(() =>
        {
            ConnectBtn.IsEnabled = false;
            DisconnectBtn.IsEnabled = true;
            JoinBtn.IsEnabled = !string.IsNullOrWhiteSpace(InviteListInput.Text);
            LeaveBtn.IsEnabled = false;
            SendBtn.IsEnabled = false;
            MessageInput.IsEnabled = false;
            IdentityInput.IsEnabled = false;
        });
    }

    void SetInGroupState()
    {
        _isConnected = false;
        Dispatcher.UIThread.Post(() =>
        {
            JoinBtn.IsEnabled = false;
            LeaveBtn.IsEnabled = true;
            SendBtn.IsEnabled = true;
            MessageInput.IsEnabled = true;
            GroupChannelInput.IsEnabled = false;
            InviteListInput.IsEnabled = false;
        });
    }

    void SetDisconnectedState()
    {
        _isConnected = false;
        Dispatcher.UIThread.Post(() =>
        {
            ConnectBtn.IsEnabled = true;
            DisconnectBtn.IsEnabled = false;
            JoinBtn.IsEnabled = false;
            LeaveBtn.IsEnabled = false;
            SendBtn.IsEnabled = false;
            MessageInput.IsEnabled = false;
            IdentityInput.IsEnabled = true;
            GroupChannelInput.IsEnabled = true;
            InviteListInput.IsEnabled = true;
        });
    }

    void OnConnect(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _identity = IdentityInput.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(_identity))
        {
            Log("Identity is required.");
            return;
        }

        try
        {
            Slim.Initialize();

            using var appName = SlimName.Parse(_identity);
            using var service = Slim.GetGlobalService();
            _app = service.CreateApp(appName, DemoConfig.DefaultSecret);

            if (!_hasConnection)
            {
                _connId = Slim.Connect(DemoConfig.DefaultServer);
                _hasConnection = true;
            }

            _app.Subscribe(_app.Name, _connId);

            _cts = new CancellationTokenSource();
            SetConnectedState(listening: true);
            SetStatus($"Connected as {_identity} (listening for invites)", Brushes.LimeGreen);
            Log($"Connected to {DemoConfig.DefaultServer} as {_identity}");
            Log("Listening for group invitations...");

            Task.Run(() => RunParticipant(_cts.Token));
        }
        catch (Exception ex)
        {
            Log($"Connection failed: {ex.Message}");
            SetStatus("Error", Brushes.Red);
        }
    }

    void OnDisconnect(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Cleanup();
        SetDisconnectedState();
        SetStatus("Disconnected", Brushes.Gray);
        UpdateParticipantsDisplay();
        Log("Disconnected.");
    }

    void OnJoin(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_app == null) return;

        var groupChannel = GroupChannelInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(groupChannel))
        {
            Log("Group channel is required.");
            return;
        }

        var inviteText = InviteListInput.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(inviteText))
        {
            Log("Already listening for invitations. Fill in the Invite field to create a group as moderator.");
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var invitees = inviteText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        SetStatus("Creating group (MLS)...", Brushes.Orange);
        Task.Run(() => RunModerator(groupChannel, invitees, ct), ct);
    }

    void OnLeave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cts?.Cancel();

        if (_session != null && _app != null)
        {
            try { _app.DeleteSession(_session); } catch { /* best effort */ }
            _session = null;
        }

        _knownParticipants.Clear();
        UpdateParticipantsDisplay();
        Log("Left the group.");

        if (_app != null)
        {
            _cts = new CancellationTokenSource();
            SetConnectedState(listening: true);
            SetStatus($"Connected as {_identity} (listening for invites)", Brushes.LimeGreen);
            Log("Listening for group invitations...");
            Task.Run(() => RunParticipant(_cts.Token));
        }
        else
        {
            SetConnectedState();
            SetStatus("Connected (left group)", Brushes.LimeGreen);
        }
    }

    void OnInviteListChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isConnected)
            JoinBtn.IsEnabled = !string.IsNullOrWhiteSpace(InviteListInput.Text);
    }

    void OnSend(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SendMessage();

    void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            SendMessage();
    }

    void SendMessage()
    {
        var text = MessageInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || _session == null) return;

        Dispatcher.UIThread.Post(() => MessageInput.Text = "");

        var payload = $"{_identity}: {text}";
        Log(payload);

        Task.Run(async () =>
        {
            try
            {
                await _session.PublishAsync(payload);
            }
            catch (Exception ex)
            {
                Log($"Send failed: {ex.Message}");
            }
        });
    }

    async Task RunModerator(string groupChannel, string[] invitees, CancellationToken ct)
    {
        try
        {
            using var channelName = SlimName.Parse(groupChannel);

            var config = new SlimSessionConfig
            {
                SessionType = SlimSessionType.Group,
                EnableMls = true,
            };

            Log("Creating group session with MLS encryption...");
            _session = await _app!.CreateSessionAsync(channelName, config, ct);
            Log($"Group session created (ID: {_session.SessionId})");

            foreach (var invitee in invitees)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var inviteeName = SlimName.Parse(invitee);
                    _app.SetRoute(inviteeName, _connId);
                    _session.Invite(inviteeName);
                    Log($"Invited {invitee}");
                }
                catch (Exception ex)
                {
                    Log($"Failed to invite {invitee}: {ex.Message}");
                }
            }

            SetInGroupState();
            SetStatus("In group (moderator, MLS)", Brushes.LimeGreen);
            Log("You are the moderator. Waiting for messages...");

            await ReceiveLoop(ct);

            _session = null;
            _knownParticipants.Clear();
            UpdateParticipantsDisplay();

            if (!ct.IsCancellationRequested)
            {
                _cts = new CancellationTokenSource();
                SetConnectedState(listening: true);
                SetStatus($"Connected as {_identity} (listening for invites)", Brushes.LimeGreen);
                Log("Session ended. Listening for new invitations...");
                await RunParticipant(_cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log($"Moderator error: {ex.Message}");
            SetStatus("Error", Brushes.Red);
        }
    }

    async Task RunParticipant(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                Log("Listening for group invitation...");
                _session = await _app!.ListenForSessionAsync(cancellationToken: ct);
                Log($"Joined group session (ID: {_session.SessionId})");

                SetInGroupState();
                SetStatus("In group (participant, MLS)", Brushes.LimeGreen);

                await ReceiveLoop(ct);

                _session = null;
                _knownParticipants.Clear();
                UpdateParticipantsDisplay();

                if (ct.IsCancellationRequested) break;

                SetConnectedState(listening: true);
                SetStatus($"Connected as {_identity} (listening for invites)", Brushes.LimeGreen);
                Log("Session ended. Listening for new invitations...");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log($"Participant error: {ex.Message}");
            SetStatus("Error", Brushes.Red);
        }
    }

    async Task ReceiveLoop(CancellationToken ct)
    {
        RefreshParticipants();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var msg = await _session!.GetMessageAsync(TimeSpan.FromSeconds(2), ct);
                Log(msg.Text);
                RefreshParticipants();
            }
            catch when (ct.IsCancellationRequested) { break; }
            catch (SlimException ex) when (ex.IsTimeout)
            {
                RefreshParticipants();
            }
            catch (SlimException ex) when (ex.IsClosed)
            {
                Log($"Session closed: {ex.Message}");
                RefreshParticipants();
                break;
            }
            catch (SlimException ex)
            {
                Log($"Session event: {ex.Message}");
                RefreshParticipants();
            }
        }
    }

    void RefreshParticipants()
    {
        if (_session == null) return;

        try
        {
            var current = _session.GetParticipants();
            var currentSet = new HashSet<string>(current.Select(p => p.ToString()));

            foreach (var p in currentSet)
            {
                if (_knownParticipants.Add(p))
                    Log($"** {p} joined the group **");
            }

            foreach (var p in _knownParticipants.ToList())
            {
                if (!currentSet.Contains(p))
                {
                    _knownParticipants.Remove(p);
                    Log($"** {p} left the group **");
                }
            }

            UpdateParticipantsDisplay(current);
        }
        catch { /* best effort */ }
    }

    void Cleanup()
    {
        _cts?.Cancel();

        if (_session != null && _app != null)
        {
            try { _app.DeleteSession(_session); } catch { /* best effort */ }
            _session = null;
        }

        _app?.Destroy();
        _app = null;
        _knownParticipants.Clear();
    }

    protected override void OnClosed(EventArgs e)
    {
        Cleanup();
        base.OnClosed(e);
    }
}
