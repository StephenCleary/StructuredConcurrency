using ChatApi.Messages;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static ChatApi.Internals.MessageSerialization;

namespace ChatApi
{
    public sealed class ChatConnection
    {
        private readonly IPipelineSocket _pipelineSocket;
        private readonly TimeSpan _keepaliveTimeSpan;
        private readonly Channel<IMessage> _inputChannel;
        private readonly Channel<IMessage> _outputChannel;
        private readonly Timer _timer;
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _outstandingRequests = new();

        public ChatConnection(IPipelineSocket pipelineSocket, TimeSpan keepaliveTimeSpan = default)
        {
            keepaliveTimeSpan = keepaliveTimeSpan == default ? TimeSpan.FromSeconds(5) : keepaliveTimeSpan;

            _pipelineSocket = pipelineSocket;
            _keepaliveTimeSpan = keepaliveTimeSpan;
            _inputChannel = Channel.CreateBounded<IMessage>(4);
            _outputChannel = Channel.CreateBounded<IMessage>(4);
            _timer = new Timer(_ => SendKeepaliveMessage(), null, keepaliveTimeSpan, Timeout.InfiniteTimeSpan);
            PipelineToChannelAsync();
            ChannelToPipelineAsync();
        }

        public Socket Socket => _pipelineSocket.Socket;
        public IPEndPoint RemoteEndPoint => _pipelineSocket.RemoteEndPoint;

        public void Complete() => _outputChannel.Writer.Complete();

        public Task SetNicknameAsync(string nickname)
        {
            var setNicknameRequestMessage = new SetNicknameRequestMessage(Guid.NewGuid(), nickname);
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _outstandingRequests.TryAdd(setNicknameRequestMessage.RequestId, tcs);
            return SendMessageAndWaitForResponseAsync();

            async Task SendMessageAndWaitForResponseAsync()
            {
                await SendMessageAsync(setNicknameRequestMessage);
                await tcs.Task;
            }
        }

        public async Task SendMessageAsync(IMessage message)
        {
            await _outputChannel.Writer.WriteAsync(message);
            _timer.Change(_keepaliveTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public IAsyncEnumerable<IMessage> InputMessages => _inputChannel.Reader.ReadAllAsync();

        private void SendKeepaliveMessage()
        {
            _outputChannel.Writer.TryWrite(new KeepaliveMessage());
            _timer.Change(_keepaliveTimeSpan, Timeout.InfiniteTimeSpan);
        }

        private void Close(Exception? exception = null)
        {
            _timer.Dispose();
            _inputChannel.Writer.TryComplete(exception);
            _pipelineSocket.Output.CancelPendingFlush();
            _pipelineSocket.Output.Complete();
        }

        private async void ChannelToPipelineAsync()
        {
            try
            {
                await foreach (var message in _outputChannel.Reader.ReadAllAsync())
                {
                    WriteMessage(message, _pipelineSocket.Output);

                    var flushResult = await _pipelineSocket.Output.FlushAsync();
                    if (flushResult.IsCanceled)
                        break;
                }

                Close();
            }
            catch (Exception exception)
            {
                Close(exception);
            }
        }

        private async void PipelineToChannelAsync()
        {
            try
            {
                while (true)
                {
                    var data = await _pipelineSocket.Input.ReadAsync();
                    var (messages, consumedPosition) = ParseMessages(data.Buffer);
                    _pipelineSocket.Input.AdvanceTo(consumedPosition, data.Buffer.End);

                    foreach (var message in messages)
                    {
                        if (message is KeepaliveMessage)
                        {
                            // Ignore
                        }
                        else if (message is AckResponseMessage ackResponseMessage)
                        {
                            if (!_outstandingRequests.TryRemove(ackResponseMessage.RequestId, out var tcs))
                            {
                                // The server sent us an ack response for a request we didn't send.
                                throw new InvalidOperationException("Protocol violation.");
                            }

                            tcs.TrySetResult();
                        }
                        else if (message is NakResponseMessage nakResponseMessage)
                        {
                            if (!_outstandingRequests.TryRemove(nakResponseMessage.RequestId, out var tcs))
                            {
                                // The server sent us an ack response for a request we didn't send.
                                throw new InvalidOperationException("Protocol violation.");
                            }

                            tcs.TrySetException(new Exception(nakResponseMessage.Message));
                        }
                        else
                        {
                            await _inputChannel.Writer.WriteAsync(message);
                        }
                    }

                    if (data.IsCompleted)
                    {
                        Close();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Close(ex);
            }
        }

        private (IReadOnlyList<IMessage> Messages, SequencePosition Consumed)
            ParseMessages(ReadOnlySequence<byte> buffer)
        {
            var result = new List<IMessage>();
            var sequenceReader = new SequenceReader<byte>(buffer);
            var consumedPosition = sequenceReader.Position;

            while (sequenceReader.Remaining != 0)
            {
                if (!sequenceReader.TryReadMessage(_pipelineSocket.MaxMessageSize, out var message))
                    break;

                // Ignore unknown message types. (`message` == `null`)
                if (message != null)
                    result.Add(message);

                consumedPosition = sequenceReader.Position;
            }

            return (result, consumedPosition);
        }
    }
}
