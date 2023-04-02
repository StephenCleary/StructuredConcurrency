using ChatApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer
{
    public sealed class ClientChatConnection
    {
        public ClientChatConnection(ChatConnection chatConnection)
        {
            ChatConnection = chatConnection;
        }

        public ChatConnection ChatConnection { get; }

        public string? Nickname { get; set; }
    }
}
