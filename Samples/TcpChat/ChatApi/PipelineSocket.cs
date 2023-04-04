using Nito.StructuredConcurrency;
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

        public PipelineSocket(RunTaskGroup group, Socket connectedSocket, uint maxMessageSize = 65536)
        {
            Socket = connectedSocket;
            RemoteEndPoint = (IPEndPoint) connectedSocket.RemoteEndPoint!;
            MaxMessageSize = maxMessageSize;
            _outputPipe = new Pipe();
            _inputPipe = new Pipe(new PipeOptions(pauseWriterThreshold: maxMessageSize + LengthPrefixLength));
            _completion = new TaskCompletionSource<object>();

            group.Run(_ => PipelineToSocketAsync(_outputPipe.Reader, Socket));
            group.Run(ct => SocketToPipelineAsync(Socket, _inputPipe.Writer, ct));
        }

        public Socket Socket { get; }

        public uint MaxMessageSize { get; }

        public IPEndPoint RemoteEndPoint { get; }

        public PipeWriter Output => _outputPipe.Writer;
        public PipeReader Input => _inputPipe.Reader;

        private static async ValueTask SocketToPipelineAsync(Socket socket, PipeWriter pipeWriter, CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    var buffer = pipeWriter.GetMemory();
                    var bytesRead = await SocketEx.TranslateExceptions(() => socket.ReceiveAsync(buffer, SocketFlags.None, CancellationToken.None));
                    if (bytesRead == 0) // Graceful close
                        break;

                    pipeWriter.Advance(bytesRead);
                    await pipeWriter.FlushAsync(cancellationToken);
                }

                pipeWriter.Complete();
            }
            catch (Exception ex)
            {
                pipeWriter.Complete(ex);
                throw;
            }
        }

        private static async ValueTask PipelineToSocketAsync(PipeReader pipeReader, Socket socket)
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
                        var bytesSent = await SocketEx.TranslateExceptions(() => socket.SendAsync(memory, SocketFlags.None));
                        buffer = buffer.Slice(bytesSent);
                        if (bytesSent != memory.Length)
                            break;
                    }

                    pipeReader.AdvanceTo(buffer.Start);

                    if (result.IsCompleted)
                        break;
                }
            }
            finally
            {
                SocketEx.ShutdownAndClose(socket);
            }
        }
    }
}