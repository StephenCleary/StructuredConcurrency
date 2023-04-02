using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApi.Messages
{
    public sealed class NakResponseMessage : IMessage
    {
        public NakResponseMessage(Guid requestId, string message)
        {
            RequestId = requestId;
            Message = message;
        }

        public Guid RequestId { get; }
        public string Message { get; }
    }
}
