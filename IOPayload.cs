using System.Text;
namespace SocketIO {
    public class IOPayload : IOData {
        private IOPacket packet;

        // 数据包
        public IOPayload (string packetType, string absolutePath, int packetId, string eventName, string text) {
            packet = new IOPacket (packetType, absolutePath, packetId, eventName, text);
        }

        // 空包
        public IOPayload (string packetType, string absolutePath) {
            packet = new IOPacket (packetType, absolutePath);
        }

        // 执行运程函数包
        public IOPayload (string packetType, int packetId, string absolutePath, string text) {
            packet = new IOPacket (packetType, packetId, absolutePath, text);
        }

        //<length1>:<packet1>[<length2>:<packet2>[...]]
        // length: 表示packet的字符长度
        // packet: 真实数据包
        public string getData () {
            string data = packet.getData ();
            return data.Length + ":" + data;
        }
    }
}