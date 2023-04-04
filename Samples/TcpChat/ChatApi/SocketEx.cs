using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatApi;

public static class SocketEx
{
    public static async ValueTask<T> TranslateExceptions<T>(Func<ValueTask<T>> work)
    {
        try
        {
            return await work().ConfigureAwait(false);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
        {
            throw new OperationCanceledException(ex.Message, ex);
        }
    }

    public static void ShutdownAndClose(Socket socket)
    {
        try
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }
        catch
        {
        }
    }
}
