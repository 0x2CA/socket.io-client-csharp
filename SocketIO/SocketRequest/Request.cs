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
        private TimeSpan _connectTimeout;

        // 包序列
        private int _packetId = -1;

        //缓冲区大小
        private int _sendChunkSize = 1024;

        // Task取消令牌
        private CancellationTokenSource _tokenSource;

        // socket 对象
        private ClientWebSocket _socket;

        // 请求数据包格式
        private RequestMode _mode = RequestMode.Packet;

        // 连接地址
        private Uri _url;

        //绝对地址
        private string _absolutePath = "";

        // 连接附加参数
        private Dictionary<string, string> _parameters;

        // 事件中心
        private EventTarget _eventTaget;

        public Request (ClientWebSocket socket, Uri url, EventTarget eventTaget, CancellationTokenSource tokenSource) {

            //协议判断
            if (url.Scheme == "https" || url.Scheme == "http" || url.Scheme == "wss" || url.Scheme == "ws") {
                _url = url;
            } else {
                throw new ArgumentException ("Unsupported protocol");
            }

            // socket 请求地址
            if (url.AbsolutePath != "/") {
                _absolutePath = url.AbsolutePath + ",";
            } else {
                _absolutePath = "";
            }

            _connectTimeout = TimeSpan.FromSeconds (30);

            _packetId = -1;

            _socket = socket;

            _eventTaget = eventTaget;

            _tokenSource = tokenSource;
        }

        // Ping包
        public Task ping () {

            return Task.Run (() => {
                if (_socket != null && _socket.State == WebSocketState.Open) {
                    string packetType = ((int) Protocol.Ping).ToString ();
                    send (Packet.getNullPacket (_mode, packetType, _absolutePath)).Wait ();
                }
            });
        }

        // 持续Ping
        public Task pingLoop (int pingInterval) {
            return Task.Run (() => {
                //呼吸包
                while (true) {
                    if (_socket != null && _socket.State == WebSocketState.Open) {
                        Task.Delay (pingInterval).Wait ();
                        ping ().Wait ();
                        _eventTaget.invoke (SocketStockEvent.Ping);
                    } else {
                        break;
                    }
                }

            }, _tokenSource.Token);
        }

        // 连接请求
        public Task connect () {

            if (_socket != null) {
                Uri url = SocketUrl.Url.Build (_url, _parameters);

                //开始连接
                bool isExecuted = _socket.ConnectAsync (url, CancellationToken.None).Wait (_connectTimeout);

                if (!isExecuted) {
                    throw new TimeoutException ();
                }

                return Task.CompletedTask;

            } else {
                throw new Exception ("Socket is Null");
            }
        }

        // 打开请求
        public Task open () {

            return Task.Run (() => {

                string packetType = (((int) Protocol.Message).ToString () + ((int) Protocol.Open).ToString ());
                send (Packet.getNullPacket (_mode, packetType, _absolutePath)).Wait ();

            }, _tokenSource.Token);
        }

        // 发送信息
        public Task emit (string eventName, object obj) {

            return Task.Run (() => {
                _packetId++;

                string text = JsonConvert.SerializeObject (obj);

                string packetType = ((int) Protocol.Message).ToString () + ((int) Protocol.Ping).ToString ();

                send (Packet.getMessagePacket (_mode, packetType, _absolutePath, _packetId, eventName, text)).Wait ();

            }, _tokenSource.Token);
        }

        // 发生信息附带函数
        public Task emit (string eventName, object obj, ClientCallbackEventHandler callback) {

            return Task.Run (() => {
                _packetId++;

                string text = JsonConvert.SerializeObject (obj);

                string packetType = ((int) Protocol.Message).ToString () + ((int) Protocol.Ping).ToString ();

                _eventTaget.on (_packetId, callback);

                send (Packet.getMessagePacket (_mode, packetType, _absolutePath, _packetId, eventName, text)).Wait ();

            }, _tokenSource.Token);
        }

        // 运行服务器函数
        public Task run (int packetId, object obj) {

            return Task.Run (() => {
                string text = JsonConvert.SerializeObject (obj);
                string packetType = ((int) Protocol.Message).ToString () + ((int) Protocol.Pong).ToString ();
                send (Packet.getAckPacket (_mode, packetType, _absolutePath, packetId, text)).Wait ();
            }, _tokenSource.Token);
        }

        // 关闭连接
        public Task close () {

            return Task.Run (() => {
                string packetType = (((int) Protocol.Message).ToString () + ((int) Protocol.Close).ToString ());
                send (Packet.getNullPacket (_mode, packetType, _absolutePath)).Wait ();
            }, _tokenSource.Token);
        }

        private Task send (AMode content) {

            return Task.Run (() => {
                if (_socket.State == WebSocketState.Open) {
                    var messageBuffer = Encoding.UTF8.GetBytes (content.getContent ());
                    var messagesCount = (int) Math.Ceiling ((double) messageBuffer.Length / _sendChunkSize);

                    for (var i = 0; i < messagesCount; i++) {
                        int offset = _sendChunkSize * i;
                        int count = _sendChunkSize;
                        bool isEndOfMessage = (i + 1) == messagesCount;

                        if ((count * (i + 1)) > messageBuffer.Length) {
                            count = messageBuffer.Length - offset;
                        }

                        _socket.SendAsync (new ArraySegment<byte> (messageBuffer, offset, count), WebSocketMessageType.Text, isEndOfMessage, _tokenSource.Token).Wait ();
                    }
                }
            }, _tokenSource.Token);
        }

    }
}