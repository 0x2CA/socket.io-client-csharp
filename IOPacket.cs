using System.Text;
namespace SocketIO {

    public class IOPacket : IOData {

        private StringBuilder builder = new StringBuilder ();
        // 数据包
        //<packet type id>[<data>]
        // packetType _namespace packetId ["eventName",text]
        public IOPacket (string packetType, string absolutePath, int packetId, string eventName, string text) {
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
        public IOPacket (string packetType, string absolutePath) {
            builder
                .Append (packetType)
                .Append (absolutePath);
        }

        // 执行运程函数包
        public IOPacket (string packetType, int packetId, string absolutePath, string text) {
            builder
                .Append (packetType)
                .Append (absolutePath)
                .Append (packetId)
                .Append ('[')
                .Append (text)
                .Append (']');
        }

        //<包类型id>[<data>]
        public string getData () {
            return builder.ToString ();
        }
    }
}