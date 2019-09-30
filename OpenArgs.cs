using System;
using System.Collections.Generic;
namespace SocketIO {

    [Serializable]
    public class OpenArgs {
        public string Sid { get; set; }

        public List<string> Upgrades { get; set; }

        public int PingInterval { get; set; }

        public int PingTimeout { get; set; }
    }
}