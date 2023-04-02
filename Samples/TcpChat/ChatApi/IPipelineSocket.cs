using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatApi
{
    public interface IPipelineSocket : IDuplexPipe
    {
        Socket Socket { get; }
        uint MaxMessageSize { get; }
        IPEndPoint RemoteEndPoint { get; }
    }
}
