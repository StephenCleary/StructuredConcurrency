using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApi.Messages
{
    public sealed class BroadcastMessage : IMessage
    {
        public BroadcastMessage(string from, string text)
        {
            From = from;
            Text = text;
        }

        public string From { get; }
        public string Text { get; }
    }
}
