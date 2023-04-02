using ChatApi;
using ChatApi.Messages;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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

            _chatConnection = new ChatConnection(new PipelineSocket(clientSocket));

            await ProcessSocketAsync(_chatConnection);
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

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            _chatConnection?.Complete();
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

                Log.Text += $"Connection at {chatConnection.RemoteEndPoint} was disconnected.\n";
            }
            catch (Exception ex)
            {
                Log.Text += $"Exception from {chatConnection.RemoteEndPoint}: [{ex.GetType().Name}] {ex.Message}\n";
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
