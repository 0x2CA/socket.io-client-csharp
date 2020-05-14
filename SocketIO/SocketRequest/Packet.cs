using SocketIO.SocketRequest.Mode;

namespace SocketIO.SocketRequest {
    class Packet {

        public static IMode GetMessagePacket (RequestMode mode, string packetType, string absolutePath, int packetId, string eventName, string text) {
            if (mode == RequestMode.Packet) {
                PacketMode packet = new PacketMode (packetType, absolutePath, packetId, eventName, text);
                return packet;
            } else if (mode == RequestMode.Payload) {
                PacketMode packet = new PacketMode (packetType, absolutePath, packetId, eventName, text);
                PayloadMode payload = new PayloadMode (packet);
                return payload;
            } else {
                return null;
            }

        }

        public static IMode GetNullPacket (RequestMode mode, string packetType, string absolutePath) {
            if (mode == RequestMode.Packet) {
                PacketMode packet = new PacketMode (packetType, absolutePath);
                return packet;
            } else if (mode == RequestMode.Payload) {
                PacketMode packet = new PacketMode (packetType, absolutePath);
                PayloadMode payload = new PayloadMode (packet);
                return payload;
            } else {
                return null;
            }

        }

        public static IMode GetAckPacket (RequestMode mode, string packetType, string absolutePath, int packetId, string text) {
            if (mode == RequestMode.Packet) {
                PacketMode packet = new PacketMode (packetType, absolutePath, packetId, text);
                return packet;
            } else if (mode == RequestMode.Payload) {
                PacketMode packet = new PacketMode (packetType, absolutePath, packetId, text);
                PayloadMode payload = new PayloadMode (packet);
                return payload;
            } else {
                return null;
            }

        }
    }

}