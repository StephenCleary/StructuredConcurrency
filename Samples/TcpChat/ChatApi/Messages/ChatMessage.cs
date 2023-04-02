using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApi.Messages
{
    public sealed class ChatMessage : IMessage
    {
        public ChatMessage(string text) => Text = text;

        public string Text { get; }
    }
}
