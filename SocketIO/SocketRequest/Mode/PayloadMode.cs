using System.Text;
using SocketIO.SocketRequest;
namespace SocketIO.SocketRequest.Mode {
    public class PayloadMode : IMode {
        private PacketMode _Packet;

        public PayloadMode (PacketMode packet) {
            _Packet = packet;
        }

        //<length1>:<packet1>[<length2>:<packet2>[...]]
        // length: 表示packet的字符长度
        // packet: 真实数据包
        public override string GetContent () {
            string data = _Packet.GetContent ();
            return data.Length + ":" + data;
        }
    }
}