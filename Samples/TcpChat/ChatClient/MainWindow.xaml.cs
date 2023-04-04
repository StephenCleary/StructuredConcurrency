using ChatApi;
using ChatApi.Messages;
using Nito.StructuredConcurrency;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private RunTaskGroup? _group;
        private ChatConnection? _chatConnection;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await clientSocket.ConnectAsync("localhost", 33333);

            Log.Text += $"Connected to {clientSocket.RemoteEndPoint}\n";

            _group = Nito.StructuredConcurrency.Advanced.TaskGroupFactory.CreateRunTaskGroup(CancellationToken.None);
            _chatConnection = new ChatConnection(_group, new PipelineSocket(_group, clientSocket));

            _group.Run(async _ => await ProcessSocketAsync(_chatConnection));
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (_chatConnection == null)
            {
                Log.Text += "No connection!\n";
            }
            else
            {
                await _chatConnection.SendMessageAsync(new ChatMessage(chatMessageTextBox.Text));
                Log.Text += $"Sent message: {chatMessageTextBox.Text}\n";
            }
        }

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (_group == null)
                return;
            _group.CancellationTokenSource.Cancel();
            await _group.DisposeAsync();
        }

        private async Task ProcessSocketAsync(ChatConnection chatConnection)
        {
            try
            {
                await foreach (var message in chatConnection.InputMessages)
                {
                    if (message is BroadcastMessage broadcastMessage)
                    {
                        Chat.Text += $"{broadcastMessage.From}: {broadcastMessage.Text}\n";
                    }
                    else
                        Log.Text += $"Got unknown message from {chatConnection.RemoteEndPoint}.\n";
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Text += $"Exception from {chatConnection.RemoteEndPoint}: [{ex.GetType().Name}] {ex.Message}\n";
            }
            finally
            {
                Log.Text += $"Connection at {chatConnection.RemoteEndPoint} was disconnected.\n";
            }
        }

        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (_chatConnection == null)
            {
                Log.Text += "No connection!\n";
            }
            else
            {
                var nickname = nicknameTextBox.Text;
                try
                {
                    Log.Text += $"Sending nickname request for {nickname}\n";
                    await _chatConnection.SetNicknameAsync(nickname);
                    Log.Text += $"Successfully set nickname to {nickname}\n";
                }
                catch (Exception ex)
                {
                    Log.Text += $"Unable to set nickname to {nickname}: {ex.Message}\n";
                }
            }
        }
    }
}
