using System;
using System.Collections.Generic;
using System.Text;

namespace SocketIO.SocketResponse {
    [Serializable]
    public class OpenMessage {
        public string Sid { get; set; }

        public List<string> Upgrades { get; set; }

        public int PingInterval { get; set; }

        public int PingTimeout { get; set; }
    }
}