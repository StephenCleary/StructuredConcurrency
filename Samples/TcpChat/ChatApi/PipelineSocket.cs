using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using static ChatApi.Internals.MessageSerialization;

namespace ChatApi
{
    public sealed class PipelineSocket : IPipelineSocket
    {
        private readonly Pipe _outputPipe;
        private readonly Pipe _inputPipe;
        private readonly TaskCompletionSource<object> _completion;

        public PipelineSocket(Socket connectedSocket, uint maxMessageSize = 65536)
        {
            Socket = connectedSocket;
            RemoteEndPoint = (IPEndPoint) connectedSocket.RemoteEndPoint!;
            MaxMessageSize = maxMessageSize;
            _outputPipe = new Pipe();
            _inputPipe = new Pipe(new PipeOptions(pauseWriterThreshold: maxMessageSize + LengthPrefixLength));
            _completion = new TaskCompletionSource<object>();

            PipelineToSocketAsync(_outputPipe.Reader, Socket);
            SocketToPipelineAsync(Socket, _inputPipe.Writer);
        }

        public Socket Socket { get; }

        public uint MaxMessageSize { get; }

        public IPEndPoint RemoteEndPoint { get; }

        public PipeWriter Output => _outputPipe.Writer;
        public PipeReader Input => _inputPipe.Reader;

        private void Close(Exception? exception = null)
        {
            if (exception != null)
            {
                if (_completion.TrySetException(exception))
                    _inputPipe.Writer.Complete(exception);
            }
            else
            {
                if (_completion.TrySetResult(null!))
                    _inputPipe.Writer.Complete();
            }

            try
            {
                Socket.Shutdown(SocketShutdown.Both);
                Socket.Close();
            }
            catch
            {
                // Ignore
            }
        }

        private async void SocketToPipelineAsync(Socket socket, PipeWriter pipeWriter)
        {
            try
            {
                while (true)
                {
                    var buffer = pipeWriter.GetMemory();
                    var bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None);
                    if (bytesRead == 0) // Graceful close
                    {
                        Close();
                        break;
                    }

                    pipeWriter.Advance(bytesRead);
                    await pipeWriter.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                Close(ex);
            }
        }

        private async void PipelineToSocketAsync(PipeReader pipeReader, Socket socket)
        {
            try
            {
                while (true)
                {
                    ReadResult result = await pipeReader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (true)
                    {
                        var memory = buffer.First;
                        if (memory.IsEmpty)
                            break;
                        var bytesSent = await socket.SendAsync(memory, SocketFlags.None);
                        buffer = buffer.Slice(bytesSent);
                        if (bytesSent != memory.Length)
                            break;
                    }

                    pipeReader.AdvanceTo(buffer.Start);

                    if (result.IsCompleted)
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
    }
}