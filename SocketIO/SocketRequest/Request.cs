using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SocketIO.SocketEvent;
using SocketIO.SocketProtocol;
using SocketIO.SocketRequest.Mode;

namespace SocketIO.SocketRequest {
    class Request {
        // 超时时间
        private TimeSpan _ConnectTimeout;

        // 包序列
        private int _PacketId = -1;

        //缓冲区大小
        private int _SendChunkSize = 1024;

        // Task取消令牌
        private CancellationTokenSource _TokenSource;

        // socket 对象
        private ClientWebSocket _Socket;

        // 请求数据包格式
        private RequestMode _Mode = RequestMode.Packet;

        // 连接地址
        private Uri _Url;

        //绝对地址
        private string _AbsolutePath = "";

        // 连接附加参数
        private Dictionary<string, string> _Parameters = new Dictionary<string, string> ();

        // 事件中心
        private EventTarget _EventTaget;

        public Request (ClientWebSocket socket, Uri url, EventTarget eventTaget, CancellationTokenSource tokenSource) {

            //协议判断
            if (url.Scheme == "https" || url.Scheme == "http" || url.Scheme == "wss" || url.Scheme == "ws") {
                _Url = url;
            } else {
                throw new ArgumentException ("Unsupported protocol");
            }

            // socket 请求地址
            if (url.AbsolutePath != "/") {
                _AbsolutePath = url.AbsolutePath + ",";
            } else {
                _AbsolutePath = "";
            }

            _ConnectTimeout = TimeSpan.FromSeconds (30);

            _PacketId = -1;

            _Socket = socket;

            _EventTaget = eventTaget;

            _TokenSource = tokenSource;
        }

        // Ping包
        public Task Ping () {

            return Task.Run (() => {
                if (_Socket != null && _Socket.State == WebSocketState.Open) {
                    string packetType = ((int) Protocol.Ping).ToString ();
                    Send (Packet.GetNullPacket (_Mode, packetType, _AbsolutePath)).Wait ();
                }
            });
        }

        // 持续Ping
        public Task PingLoop (int pingInterval) {
            return Task.Run (() => {
                //呼吸包
                while (true) {
                    if (_Socket != null && _Socket.State == WebSocketState.Open) {
                        Task.Delay (pingInterval).Wait ();
                        Ping ().Wait ();
                        _EventTaget.Emit (SocketStockEvent.Ping);
                    } else {
                        break;
                    }
                }

            }, _TokenSource.Token);
        }

        // 连接请求
        public Task Connect () {

            if (_Socket != null) {
                Uri url = SocketUrl.Url.Build (_Url, _Parameters);

                //开始连接
                bool isExecuted = _Socket.ConnectAsync (url, CancellationToken.None).Wait (_ConnectTimeout);

                if (!isExecuted) {
                    throw new TimeoutException ();
                }

                return Task.CompletedTask;

            } else {
                throw new Exception ("Socket is Null");
            }
        }

        // 打开请求
        public Task Open () {

            return Task.Run (() => {

                string packetType = (((int) Protocol.Message).ToString () + ((int) Protocol.Open).ToString ());
                Send (Packet.GetNullPacket (_Mode, packetType, _AbsolutePath)).Wait ();

            }, _TokenSource.Token);
        }

        // 发送信息
        public Task Emit (string eventName, object obj) {

            return Task.Run (() => {
                _PacketId++;

                string text = JsonConvert.SerializeObject (obj);

                string packetType = ((int) Protocol.Message).ToString () + ((int) Protocol.Ping).ToString ();

                Send (Packet.GetMessagePacket (_Mode, packetType, _AbsolutePath, _PacketId, eventName, text)).Wait ();

            }, _TokenSource.Token);
        }

        // 发生信息附带函数
        public Task Emit (string eventName, object obj, ClientCallbackEventHandler callback) {

            return Task.Run (() => {
                _PacketId++;

                string text = JsonConvert.SerializeObject (obj);

                string packetType = ((int) Protocol.Message).ToString () + ((int) Protocol.Ping).ToString ();

                _EventTaget.On (_PacketId, callback);

                Send (Packet.GetMessagePacket (_Mode, packetType, _AbsolutePath, _PacketId, eventName, text)).Wait ();

            }, _TokenSource.Token);
        }

        // 运行服务器函数
        public Task Run (int packetId, object obj) {

            return Task.Run (() => {
                string text = JsonConvert.SerializeObject (obj);
                string packetType = ((int) Protocol.Message).ToString () + ((int) Protocol.Pong).ToString ();
                Send (Packet.GetAckPacket (_Mode, packetType, _AbsolutePath, packetId, text)).Wait ();
            }, _TokenSource.Token);
        }

        // 关闭连接
        public Task Close () {

            return Task.Run (() => {
                string packetType = (((int) Protocol.Message).ToString () + ((int) Protocol.Close).ToString ());
                Send (Packet.GetNullPacket (_Mode, packetType, _AbsolutePath)).Wait ();
            }, _TokenSource.Token);
        }

        private Task Send (IMode content) {

            return Task.Run (() => {
                if (_Socket.State == WebSocketState.Open) {
                    var messageBuffer = Encoding.UTF8.GetBytes (content.GetContent ());
                    var messagesCount = (int) Math.Ceiling ((double) messageBuffer.Length / _SendChunkSize);

                    for (var i = 0; i < messagesCount; i++) {
                        int offset = _SendChunkSize * i;
                        int count = _SendChunkSize;
                        bool isEndOfMessage = (i + 1) == messagesCount;

                        if ((count * (i + 1)) > messageBuffer.Length) {
                            count = messageBuffer.Length - offset;
                        }

                        _Socket.SendAsync (new ArraySegment<byte> (messageBuffer, offset, count), WebSocketMessageType.Text, isEndOfMessage, _TokenSource.Token).Wait ();
                    }
                }
            }, _TokenSource.Token);
        }

    }
}