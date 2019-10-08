using System.Text;
using SocketIO.SocketRequest;
namespace SocketIO.SocketRequest.Mode {
    public class PayloadMode : AMode {
        private PacketMode _packet;

        public PayloadMode (PacketMode packet) {
            _packet = packet;
        }

        //<length1>:<packet1>[<length2>:<packet2>[...]]
        // length: 表示packet的字符长度
        // packet: 真实数据包
        public override string getContent () {
            string data = _packet.getContent ();
            return data.Length + ":" + data;
        }
    }
}