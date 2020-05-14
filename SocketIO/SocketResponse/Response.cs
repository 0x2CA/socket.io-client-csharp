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
        private int _ReceiveChunkSize = 1024;

        // Task取消令牌
        private CancellationTokenSource _TokenSource;

        // socket 对象
        private ClientWebSocket _Socket;

        // 连接地址
        private Uri _Url;

        //绝对地址
        private string _AbsolutePath = "";

        // 事件中心
        private EventTarget _EventTarget;

        // 请求对象
        private Request _Request;

        // 连接状态
        public SocketState State = SocketState.Close;

        public Response (ClientWebSocket socket, Uri url, EventTarget eventTarget, CancellationTokenSource tokenSource, Request request) {

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

            _Socket = socket;

            _EventTarget = eventTarget;

            _TokenSource = tokenSource;

            _Request = request;
        }

        // socket 全局监听
        public void Listen () {

            // 断开连接监听
            Task.Run (() => {
                while (true) {
                    Task.Delay (500).Wait ();
                    if (State != SocketState.Close && State != SocketState.Abort) {

                        // 异常检测
                        if (_Socket.State == WebSocketState.Aborted) {

                            if (State != SocketState.Abort) {
                                State = SocketState.Abort;
                                _EventTarget.Emit (SocketStockEvent.Abort);
                            }

                            // 取消所有socket运行
                            _TokenSource.Cancel ();
                        } else if (_Socket.State == WebSocketState.Closed) {
                            if (State != SocketState.Close) {
                                State = SocketState.Close;
                                _EventTarget.Emit (SocketStockEvent.Close);
                            }
                            // 取消所有socket运行
                            _TokenSource.Cancel ();
                        }
                    } else {
                        break;
                    }
                }
            }, _TokenSource.Token);

            // 接收监听
            Task.Run (() => {
                // 接收缓冲区
                var buffer = new byte[_ReceiveChunkSize];
                while (true) {
                    // 打开状态
                    if (_Socket.State == WebSocketState.Open) {

                        // 等待信息传输
                        WebSocketReceiveResult result = _Socket.ReceiveAsync (new ArraySegment<byte> (buffer), _TokenSource.Token).Result;

                        // 判断数据类型
                        if (result.MessageType == WebSocketMessageType.Text) {

                            var builder = new StringBuilder ();

                            string str = Encoding.UTF8.GetString (buffer, 0, result.Count);
                            builder.Append (str);

                            // 全部接收
                            while (!result.EndOfMessage) {
                                // 接收
                                result = _Socket.ReceiveAsync (new ArraySegment<byte> (buffer), _TokenSource.Token).Result;
                                // 在为字符串
                                str = Encoding.UTF8.GetString (buffer, 0, result.Count);
                                // 追加
                                builder.Append (str);
                            }

                            ParseReceive (builder.ToString ()).Wait ();
                        } else if (result.MessageType == WebSocketMessageType.Binary) {

                        }
                    }
                }
            }, _TokenSource.Token);
        }

        // 接收解析
        private Task ParseReceive (string receive) {
            return Task.Run (() => {
                // 建立连接
                if (receive.StartsWith ("0{\"sid\":\"")) {
                    Open (receive).Wait ();
                } else if (receive == ((int) Protocol.Message).ToString () + ((int) Protocol.Open).ToString () + _AbsolutePath) {
                    //打开状态
                    if (State != SocketState.Connect) {
                        State = SocketState.Connect;
                        _EventTarget.Emit (SocketStockEvent.Connect);
                    }

                } else if (receive == ((int) Protocol.Message).ToString () + ((int) Protocol.Close).ToString () + _AbsolutePath) {
                    //关闭状态
                    if (State != SocketState.Close) {
                        State = SocketState.Close;
                        _EventTarget.Emit (SocketStockEvent.Close);
                    }
                } else if (receive == ((int) Protocol.Pong).ToString ()) {
                    // Pong
                    _EventTarget.Emit (SocketStockEvent.Pong);
                } else if (receive.StartsWith (((int) Protocol.Message).ToString () + ((int) Protocol.Ping).ToString () + _AbsolutePath)) {
                    Message (receive).Wait ();
                } else if (receive.StartsWith (((int) Protocol.Message).ToString () + ((int) Protocol.Pong).ToString () + _AbsolutePath)) {
                    Ack (receive).Wait ();
                }
            }, _TokenSource.Token);
        }

        private Task Open (string receive) {
            return Task.Run (() => {
                if (State != SocketState.Open) {
                    State = SocketState.Open;
                    _EventTarget.Emit (SocketStockEvent.Open);
                }

                string message = receive.TrimStart ('0');
                OpenMessage args = JsonConvert.DeserializeObject<OpenMessage> (message);

                _Request.Open ().Wait ();

                _Request.PingLoop (args.PingInterval);
            }, _TokenSource.Token);

        }

        private Task Message (string receive) {
            return Task.Run (() => {

                // 消息 42 _absolutePath packetid ["event","result","result"...]
                string message = receive.Substring ((((int) Protocol.Message).ToString () + ((int) Protocol.Ping).ToString () + _AbsolutePath).Length);
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

                    _EventTarget.Emit (eventName, result, delegate (object obj) {
                        if (packetId >= 0) {
                            _Request.Run (packetId, obj).Wait ();
                        }
                    });
                }
            }, _TokenSource.Token);

        }

        private Task Ack (string receive) {
            return Task.Run (() => {

                // ack消息 43 _absolutePath packetid ["result","result"...]

                string message = receive.Substring ((((int) Protocol.Message).ToString () + ((int) Protocol.Pong).ToString () + _AbsolutePath).Length);

                Regex regex = new Regex ($@"^(\d+)\[({{[\s\S]*}})\]$");

                if (regex.IsMatch (message)) {
                    var groups = regex.Match (message).Groups;
                    int packetId = int.Parse (groups[1].Value);

                    object[] results = JsonConvert.DeserializeObject<object[]> ($"[{groups[2].Value}]");
                    string result = JsonConvert.SerializeObject (results[0]);

                    if (result.StartsWith ("\"")) {
                        result = JsonConvert.DeserializeObject<string> (result);
                    }

                    _EventTarget.Emit (packetId, result);
                    _EventTarget.Off (packetId);

                }
            }, _TokenSource.Token);

        }
    }

}