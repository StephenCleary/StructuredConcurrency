using ChatApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer
{
    public sealed class ConnectionCollection
    {
        private readonly object _mutex = new();
        private readonly List<ClientChatConnection> _connections = new();

        public ClientChatConnection Add(ChatConnection connection)
        {
            lock (_mutex)
            {
                var clientConnection = new ClientChatConnection(connection);
                _connections.Add(clientConnection);
                return clientConnection;
            }
        }

        public void Remove(ChatConnection connection)
        {
            lock (_mutex)
            {
                _connections.RemoveAll(x => x.ChatConnection == connection);
            }
        }

        public bool TrySetNickname(ChatConnection connection, string nickname)
        {
            lock (_mutex)
            {
                var clientChatConnection = _connections.FirstOrDefault(x => x.ChatConnection == connection);
                if (clientChatConnection == null)
                    return false;

                if (clientChatConnection.Nickname == nickname)
                    return true;

                if (_connections.Any(x => x.Nickname == nickname))
                    return false;

                clientChatConnection.Nickname = nickname;
                return true;
            }
        }

        public IReadOnlyCollection<ClientChatConnection> CurrentConnections
        {
            get
            {
                lock (_mutex)
                {
                    return _connections.ToList();
                }
            }
        }
    }
}
