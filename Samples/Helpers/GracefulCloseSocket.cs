using System.Net.Sockets;

namespace Helpers
{
    public sealed class GracefulCloseSocket : IDisposable
    {
        public Socket Socket { get; init; } = null!;

        public void Dispose()
        {
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Dispose();
        }
    }
}
