using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using QuicFlowClient.Services;
using System;
using System.Threading.Tasks;
using System.Reactive.Concurrency;

namespace QuicFlowClient.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private readonly QuicClientService _quicService;
        
        private string _serverAddress = "127.0.0.1";
        public string ServerAddress
        {
            get => _serverAddress;
            set => this.RaiseAndSetIfChanged(ref _serverAddress, value);
        }

        private int _serverPort = 4433;
        public int ServerPort
        {
            get => _serverPort;
            set => this.RaiseAndSetIfChanged(ref _serverPort, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => this.RaiseAndSetIfChanged(ref _isConnected, value);
        }

        private string _connectionStatus = "Disconnected";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => this.RaiseAndSetIfChanged(ref _connectionStatus, value);
        }

        private string _inputMessage = "";
        public string InputMessage
        {
            get => _inputMessage;
            set => this.RaiseAndSetIfChanged(ref _inputMessage, value);
        }
        
        // Using ObservableCollection for UI binding
        public ObservableCollection<string> ChatMessages { get; } = new ObservableCollection<string>();
        // Using string for Logs to append easily as a big block, or ObservableCollection?
        // User asked for "Text Box", so typically a single string block is easier if we just append, 
        // but for performance with many logs, ObservableCollection with ItemTemplate is better.
        // Let's stick to a string property for the Log TextBox as requested "Text Box" implies content binding.
        // Actually, appending to a huge string is bad. 
        // Let's use an ObservableCollection<string> and bind it to an ItemsControl or use a converter if needed.
        // But for a simple "Text Box" view, we can just bind a string. Let's try string first for simplicity.
        
        private string _logs = "";
        public string Logs
        {
            get => _logs;
            set => this.RaiseAndSetIfChanged(ref _logs, value);
        }

        public ReactiveCommand<Unit, Unit> ToggleConnectionCommand { get; }
        public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }

        public MainWindowViewModel()
        {
            _quicService = new QuicClientService();
            _quicService.OnLog += msg => 
            {
                // Marshal to UI thread if needed (Avalonia reactive bindings usually handle this, but better safe)
                // Actually ReactiveUI properties need to be set on UI thread or standard scheduling.
                // We'll trust Dispatcher or RxApp.MainThreadScheduler.
                RxApp.MainThreadScheduler.Schedule(() => 
                {
                    Logs += msg + Environment.NewLine;
                });
            };
            
            _quicService.OnMessageReceived += msg => 
            {
                RxApp.MainThreadScheduler.Schedule(() => 
                {
                    ChatMessages.Add($"Server: {msg}");
                });
            };

            _quicService.OnConnectionLost += () =>
            {
                RxApp.MainThreadScheduler.Schedule(() => 
                {
                    IsConnected = false;
                    ConnectionStatus = "Disconnected";
                });
            };

            var canConnect = this.WhenAnyValue(x => x.ServerAddress, address => !string.IsNullOrWhiteSpace(address));

            ToggleConnectionCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (IsConnected)
                {
                    _quicService.Disconnect();
                    IsConnected = false;
                    ConnectionStatus = "Disconnected";
                }
                else
                {
                    ConnectionStatus = "Connecting...";
                    // Run on background to not freeze UI
                    await Task.Run(async () => 
                    {
                        await _quicService.ConnectAsync(ServerAddress, ServerPort);
                    });
                    
                    if (_quicService.IsConnected)
                    {
                        IsConnected = true;
                        ConnectionStatus = "Connected";
                    }
                    else
                    {
                        ConnectionStatus = "Connection Failed";
                        IsConnected = false;
                    }
                }
            }, canConnect);

            var canSend = this.WhenAnyValue(x => x.IsConnected, x => x.InputMessage, 
                (connected, msg) => connected && !string.IsNullOrWhiteSpace(msg));

            SendMessageCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (!string.IsNullOrWhiteSpace(InputMessage))
                {
                    string msg = InputMessage;
                    InputMessage = ""; // Clear input
                    ChatMessages.Add($"Me: {msg}");
                    await _quicService.SendMessageAsync(msg);
                }
            }, canSend);
        }
    }
}
