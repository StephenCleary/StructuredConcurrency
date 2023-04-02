using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApi.Messages
{
    public sealed class SetNicknameRequestMessage : IMessage
    {
        public SetNicknameRequestMessage(Guid requestId, string nickname)
        {
            RequestId = requestId;
            Nickname = nickname;
        }

        public Guid RequestId { get; }
        public string Nickname { get; }
    }
}
