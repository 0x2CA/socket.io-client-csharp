using System.Text;
using SocketIO.SocketRequest;
namespace SocketIO.SocketRequest.Mode {

    public class PacketMode : AMode {

        private StringBuilder builder = new StringBuilder ();
        // 数据包
        //<packet type id>[<data>]
        // packetType _namespace packetId ["eventName",text]
        public PacketMode (string packetType, string absolutePath, int packetId, string eventName, string text) {
            builder
                .Append (packetType)
                .Append (absolutePath)
                .Append (packetId)
                .Append ('[')
                .Append ('"')
                .Append (eventName)
                .Append ('"')
                .Append (',')
                .Append (text)
                .Append (']');
        }

        // 空包
        public PacketMode (string packetType, string absolutePath) {
            builder
                .Append (packetType)
                .Append (absolutePath);
        }

        // 执行运程函数包
        public PacketMode (string packetType, string absolutePath, int packetId, string text) {
            builder
                .Append (packetType)
                .Append (absolutePath)
                .Append (packetId)
                .Append ('[')
                .Append (text)
                .Append (']');
        }

        //<包类型id>[<data>]
        public override string getContent () {
            return builder.ToString ();
        }

    }
}