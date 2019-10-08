using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SocketIO.SocketEvent;
using SocketIO.SocketProtocol;
using SocketIO.SocketRequest;

namespace SocketIO.SocketResponse {
    class Response {

        //缓冲区大小
        private int _receiveChunkSize = 1024;

        // Task取消令牌
        private CancellationTokenSource _tokenSource;

        // socket 对象
        private ClientWebSocket _socket;

        // 连接地址
        private Uri _url;

        //绝对地址
        private string _absolutePath = "";

        // 事件中心
        private EventTarget _eventTarget;

        // 请求对象
        private Request _request;

        // 连接状态
        public SocketState state = SocketState.Close;

        public Response (ClientWebSocket socket, Uri url, EventTarget eventTarget, CancellationTokenSource tokenSource, Request request) {

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

            _socket = socket;

            _eventTarget = eventTarget;

            _tokenSource = tokenSource;

            _request = request;
        }

        // socket 全局监听
        public void listen () {

            // 断开连接监听
            Task.Run (() => {
                while (true) {
                    Task.Delay (500).Wait ();
                    if (state != SocketState.Close && state != SocketState.Abort) {

                        // 异常检测
                        if (_socket.State == WebSocketState.Aborted) {

                            if (state != SocketState.Abort) {
                                state = SocketState.Abort;
                                _eventTarget.invoke (SocketStockEvent.Abort);
                            }

                            // 取消所有socket运行
                            _tokenSource.Cancel ();
                        } else if (_socket.State == WebSocketState.Closed) {
                            if (state != SocketState.Close) {
                                state = SocketState.Close;
                                _eventTarget.invoke (SocketStockEvent.Close);
                            }
                            // 取消所有socket运行
                            _tokenSource.Cancel ();
                        }
                    } else {
                        break;
                    }
                }
            }, _tokenSource.Token);

            // 接收监听
            Task.Run (() => {
                // 接收缓冲区
                var buffer = new byte[_receiveChunkSize];
                while (true) {
                    // 打开状态
                    if (_socket.State == WebSocketState.Open) {

                        // 等待信息传输
                        WebSocketReceiveResult result = _socket.ReceiveAsync (new ArraySegment<byte> (buffer), _tokenSource.Token).Result;

                        // 判断数据类型
                        if (result.MessageType == WebSocketMessageType.Text) {

                            var builder = new StringBuilder ();

                            string str = Encoding.UTF8.GetString (buffer, 0, result.Count);
                            builder.Append (str);

                            // 全部接收
                            while (!result.EndOfMessage) {
                                // 接收
                                result = _socket.ReceiveAsync (new ArraySegment<byte> (buffer), _tokenSource.Token).Result;
                                // 在为字符串
                                str = Encoding.UTF8.GetString (buffer, 0, result.Count);
                                // 追加
                                builder.Append (str);
                            }

                            parseReceive (builder.ToString ()).Wait ();
                        } else if (result.MessageType == WebSocketMessageType.Binary) {

                        }
                    }
                }
            }, _tokenSource.Token);
        }

        // 接收解析
        private Task parseReceive (string receive) {
            return Task.Run (() => {
                // 建立连接
                if (receive.StartsWith ("0{\"sid\":\"")) {
                    open (receive).Wait ();
                } else if (receive == ((int) Protocol.Message).ToString () + ((int) Protocol.Open).ToString () + _absolutePath) {
                    //打开状态
                    if (state != SocketState.Connect) {
                        state = SocketState.Connect;
                        _eventTarget.invoke (SocketStockEvent.Connect);
                    }

                } else if (receive == ((int) Protocol.Message).ToString () + ((int) Protocol.Close).ToString () + _absolutePath) {
                    //关闭状态
                    if (state != SocketState.Close) {
                        state = SocketState.Close;
                        _eventTarget.invoke (SocketStockEvent.Close);
                    }
                } else if (receive == ((int) Protocol.Pong).ToString ()) {
                    // Pong
                    _eventTarget.invoke (SocketStockEvent.Pong);
                } else if (receive.StartsWith (((int) Protocol.Message).ToString () + ((int) Protocol.Ping).ToString () + _absolutePath)) {
                    message (receive).Wait ();
                } else if (receive.StartsWith (((int) Protocol.Message).ToString () + ((int) Protocol.Pong).ToString () + _absolutePath)) {
                    ack (receive).Wait ();
                }
            }, _tokenSource.Token);
        }

        private Task open (string receive) {
            return Task.Run (() => {
                if (state != SocketState.Open) {
                    state = SocketState.Open;
                    _eventTarget.invoke (SocketStockEvent.Open);
                }

                string message = receive.TrimStart ('0');
                OpenMessage args = JsonConvert.DeserializeObject<OpenMessage> (message);

                _request.open ().Wait ();

                _request.pingLoop (args.PingInterval);
            }, _tokenSource.Token);

        }

        private Task message (string receive) {
            return Task.Run (() => {

                // 消息 42 _absolutePath packetid ["event","result","result"...]
                string message = receive.Substring ((((int) Protocol.Message).ToString () + ((int) Protocol.Ping).ToString () + _absolutePath).Length);
                Regex regex = new Regex ($@"^(\d*)\[""([*\s\w-]+)"",([\s\S]*)\]$");
                if (regex.IsMatch (message)) {
                    var groups = regex.Match (message).Groups;

                    int packetId = -1;
                    if (groups[1].Value != "") {
                        packetId = int.Parse (groups[1].Value);
                    }

                    string eventName = groups[2].Value;
                    //["event","result","result"...]
                    object[] results = JsonConvert.DeserializeObject<object[]> ($"[{groups[3].Value}]");
                    string result = JsonConvert.SerializeObject (results[0]);

                    if (result.StartsWith ("\"")) {
                        result = JsonConvert.DeserializeObject<string> (result);
                    }

                    _eventTarget.invoke (eventName, result, delegate (object obj) {
                        if (packetId >= 0) {
                            _request.run (packetId, obj).Wait ();
                        }
                    });
                }
            }, _tokenSource.Token);

        }

        private Task ack (string receive) {
            return Task.Run (() => {

                // ack消息 43 _absolutePath packetid ["result","result"...]

                string message = receive.Substring ((((int) Protocol.Message).ToString () + ((int) Protocol.Pong).ToString () + _absolutePath).Length);

                Regex regex = new Regex ($@"^(\d+)\[({{[\s\S]*}})\]$");

                if (regex.IsMatch (message)) {
                    var groups = regex.Match (message).Groups;
                    int packetId = int.Parse (groups[1].Value);

                    object[] results = JsonConvert.DeserializeObject<object[]> ($"[{groups[2].Value}]");
                    string result = JsonConvert.SerializeObject (results[0]);

                    if (result.StartsWith ("\"")) {
                        result = JsonConvert.DeserializeObject<string> (result);
                    }

                    _eventTarget.invoke (packetId, result);
                    _eventTarget.off (packetId);

                }
            }, _tokenSource.Token);

        }
    }

}