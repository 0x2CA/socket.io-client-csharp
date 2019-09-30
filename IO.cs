using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SocketIO {

    public class IO {

        public IO (Uri url) {
            //协议判断
            if (url.Scheme == "https" || url.Scheme == "http" || url.Scheme == "wss" || url.Scheme == "ws") {
                _url = url;
            } else {
                throw new ArgumentException ("Unsupported protocol");
            }

            // 事件字典
            _generalEventHandlers = new Dictionary<string, GeneralEventHandlers> ();
            _stockEventHandlers = new Dictionary<IOEvent, StockEventHandlers> ();
            _callbackEventHandlers = new Dictionary<int, GeneralEventHandlers> ();

            //包序列
            _packetId = -1;

            // socket 请求地址
            if (url.AbsolutePath != "/") {
                _absolutePath = url.AbsolutePath + ",";
            } else {
                _absolutePath = "";
            }

            _connectTimeout = TimeSpan.FromSeconds (30);
        }

        public IO (string url) : this (new Uri (url)) { }

        // 连接地址
        private Uri _url;

        //绝对地址
        private string _absolutePath = "";

        // 连接附加参数
        private Dictionary<string, string> _parameters;

        // 监听事件
        private Dictionary<string, GeneralEventHandlers> _generalEventHandlers;

        // 监听内置事件
        private Dictionary<IOEvent, StockEventHandlers> _stockEventHandlers;

        //服务器执行事件
        private Dictionary<int, GeneralEventHandlers> _callbackEventHandlers;

        // 超时时间
        private TimeSpan _connectTimeout;

        private int _eio = 3;

        // 包序列
        private int _packetId = -1;

        //缓冲区大小
        private int _receiveChunkSize = 1024;
        private int _sendChunkSize = 1024;

        // Task取消令牌
        private CancellationTokenSource _tokenSource;

        // socket 对象
        private ClientWebSocket _socket;

        // 连接状态
        private IOState _state = IOState.CLOSED;

        //连接状态
        public IOState getState () {
            return _state;
        }

        // 添加内置事件监听（可重复监听）
        public void on (IOEvent eventName, StockEventHandlers callback) {
            if (_stockEventHandlers.ContainsKey (eventName)) {
                StockEventHandlers list;
                bool result = _stockEventHandlers.TryGetValue (eventName, out list);
                if (result && list != null) {
                    list += callback;
                    _stockEventHandlers.Remove (eventName);
                    _stockEventHandlers.Add (eventName, list);
                } else {
                    _stockEventHandlers.Remove (eventName);
                    _stockEventHandlers.Add (eventName, callback);
                }
            } else {
                _stockEventHandlers.Add (eventName, callback);
            }
        }

        // 添加一般事件监听（可重复监听）
        public void on (string eventName, GeneralEventHandlers callback) {
            if (_generalEventHandlers.ContainsKey (eventName)) {
                GeneralEventHandlers list;
                bool result = _generalEventHandlers.TryGetValue (eventName, out list);
                if (result && list != null) {
                    list += callback;
                    _generalEventHandlers.Remove (eventName);
                    _generalEventHandlers.Add (eventName, list);
                } else {
                    _generalEventHandlers.Remove (eventName);
                    _generalEventHandlers.Add (eventName, callback);
                }
            } else {
                _generalEventHandlers.Add (eventName, callback);
            }
        }

        // 移除内置事件监听
        public void off (IOEvent eventName, StockEventHandlers callback) {
            if (_stockEventHandlers.ContainsKey (eventName)) {
                StockEventHandlers list;
                bool result = _stockEventHandlers.TryGetValue (eventName, out list);
                if (result && list != null) {
                    list -= callback;
                    _stockEventHandlers.Remove (eventName);
                    _stockEventHandlers.Add (eventName, list);
                } else {
                    _stockEventHandlers.Remove (eventName);
                }
            }
        }

        // 移除一般事件监听
        public void off (string eventName, GeneralEventHandlers callback) {
            if (_generalEventHandlers.ContainsKey (eventName)) {
                GeneralEventHandlers list;
                bool result = _generalEventHandlers.TryGetValue (eventName, out list);
                if (result && list != null) {
                    list -= callback;
                    _generalEventHandlers.Remove (eventName);
                    _generalEventHandlers.Add (eventName, list);
                } else {
                    _generalEventHandlers.Remove (eventName);
                }
            }
        }

        // 调用内置事件监听函数
        private void invoke (IOEvent eventName) {
            if (_stockEventHandlers.ContainsKey (eventName)) {
                StockEventHandlers list;
                bool isExist = _stockEventHandlers.TryGetValue (eventName, out list);
                if (isExist && list != null) {
                    list.Invoke ();
                } else {
                    _stockEventHandlers.Remove (eventName);
                }
            }
        }

        // 调用一般事件监听函数
        private void invoke (string eventName, string result, CallbackEventHandler callback) {
            if (_generalEventHandlers.ContainsKey (eventName)) {
                GeneralEventHandlers list;
                bool isExist = _generalEventHandlers.TryGetValue (eventName, out list);
                if (isExist && list != null) {
                    list.Invoke (result, callback);
                } else {
                    _generalEventHandlers.Remove (eventName);
                }
            }
        }

        public Task emit (string eventName, object obj, GeneralEventHandlers callback) {

            return Task.Run (() => {
                // 计时加1
                _packetId++;

                if (_callbackEventHandlers.ContainsKey (_packetId)) {
                    _callbackEventHandlers.Remove (_packetId);
                    _callbackEventHandlers.Add (_packetId, callback);
                } else {
                    _callbackEventHandlers.Add (_packetId, callback);
                }

                string message = buildMessage (eventName, obj).Result;

                if (_state == IOState.CONNECTED) {
                    send (message).Wait ();
                } else {
                    throw new InvalidOperationException ("Socket connection not ready, emit failure.");
                }

            }, _tokenSource.Token);

        }

        public Task emit (string eventName, object obj) {
            return Task.Run (() => {

                // 计时加1
                _packetId++;

                string message = buildMessage (eventName, obj).Result;

                if (_state == IOState.CONNECTED) {
                    send (message).Wait ();
                } else {
                    throw new InvalidOperationException ("Socket connection not ready, emit failure.");
                }

            }, _tokenSource.Token);

        }

        // 构建数据包
        private Task<string> buildMessage (string eventName, object obj) {
            return Task.Run (() => {
                string text = JsonConvert.SerializeObject (obj);

                string packetType = ((int) IOProtocol.Message).ToString () + ((int) IOProtocol.Ping).ToString ();

                IOPacket packet = new IOPacket (packetType, _absolutePath, _packetId, eventName, text);

                string message = packet.getData ();

                return message;
            }, _tokenSource.Token);
        }

        // 构建空包
        private Task<string> buildNull (string packetType) {
            return Task.Run (() => {
                IOPacket packet = new IOPacket (packetType, _absolutePath);

                string message = packet.getData ();

                return message;
            }, _tokenSource.Token);
        }

        private Task<string> buildCallback (int packetId, object obj) {
            return Task.Run (() => {

                string text = JsonConvert.SerializeObject (obj);

                IOPacket packet = new IOPacket (((int) IOProtocol.Message).ToString () + ((int) IOProtocol.Pong).ToString (), packetId, _absolutePath, text);

                string message = packet.getData ();

                return message;
            }, _tokenSource.Token);
        }

        // 发送原始信息
        private Task send (string text) {

            return Task.Run (() => {
                if (_socket.State == WebSocketState.Open) {
                    var messageBuffer = Encoding.UTF8.GetBytes (text);
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

        // 发起连接
        public Task connect () {

            // 取消令牌生成
            _tokenSource = new CancellationTokenSource ();

            // 构建请求地址
            Uri url = buildUrl (_url, _eio.ToString (), _parameters);

            if (_socket != null) {
                _socket.Dispose ();
            }

            _socket = new ClientWebSocket ();

            //开始连接
            bool isExecuted = _socket.ConnectAsync (url, CancellationToken.None).Wait (_connectTimeout);

            if (!isExecuted) {
                throw new TimeoutException ();
            }

            listing ();

            return Task.CompletedTask;
        }

        // 关闭连接
        public Task close () {
            if (_socket == null) {
                throw new InvalidOperationException ("Close failed, must connect first.");
            } else {
                _tokenSource.Cancel ();
                _tokenSource.Dispose ();
                _socket.Abort ();
                _socket.Dispose ();
                _socket = null;
                invoke (IOEvent.CLOSE);
                return Task.CompletedTask;
            }
        }

        // socket 全局监听
        private void listing () {

            // 断开连接监听
            Task.Run (() => {
                while (true) {
                    Task.Delay (500).Wait ();

                    // 异常检测
                    if (_socket.State == WebSocketState.Aborted) {
                        _state = IOState.ABORTED;
                        invoke (IOEvent.ABORTED);
                        // 取消所有socket运行
                        _tokenSource.Cancel ();
                    } else if (_socket.State == WebSocketState.Closed) {
                        _state = IOState.CLOSED;
                        invoke (IOEvent.CLOSE);
                        // 取消所有socket运行
                        _tokenSource.Cancel ();
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

        // 呼吸包
        private void ping (int pingInterval) {
            Task.Run (() => {
                //呼吸包
                while (true) {
                    if (_state == IOState.CONNECTED) {
                        Task.Delay (pingInterval).Wait ();
                        send (((int) IOProtocol.Ping).ToString ()).Wait ();
                        invoke (IOEvent.PING);
                    } else {
                        break;
                    }
                }
            }, _tokenSource.Token);
        }

        // 构建完整地址
        private Uri buildUrl (Uri url, string eio, Dictionary<string, string> parameters) {
            var builder = new StringBuilder ();

            // 协议替换
            if (url.Scheme == "https" || url.Scheme == "wss") {
                builder.Append ("wss://");
            } else {
                builder.Append ("ws://");
            }

            // 域名
            builder.Append (url.Host);

            // 端口
            if (!url.IsDefaultPort) {
                builder.Append (":").Append (url.Port);
            }

            // 地址构建
            builder
                .Append ("/socket.io/?EIO=")
                .Append (eio)
                .Append ("&transport=websocket");

            //附加参数
            if (parameters != null) {
                foreach (var item in parameters) {
                    builder
                        .Append ("&")
                        .Append (item.Key)
                        .Append ("=")
                        .Append (item.Value);
                }
            }

            return new Uri (builder.ToString ());
        }

        // 建立连接
        private Task open () {
            return Task.Run (() => {
                string message = buildNull (((int) IOProtocol.Message).ToString () + ((int) IOProtocol.Open).ToString ()).Result;
                send (message).Wait ();
            }, _tokenSource.Token);
        }

        // 接收解析
        private Task parseReceive (string receive) {
            return Task.Run (() => {
                // 建立连接
                if (receive.StartsWith ("0{\"sid\":\"")) {
                    _state = IOState.OPEN;
                    invoke (IOEvent.OPEN);

                    string message = receive.TrimStart ('0');
                    OpenArgs args = JsonConvert.DeserializeObject<OpenArgs> (message);

                    open ().Wait ();

                    if (_state != IOState.CONNECTED) {
                        _state = IOState.CONNECTED;
                        invoke (IOEvent.CONNECT);
                    }

                    ping (args.PingInterval);
                } else if (receive == ((int) IOProtocol.Message).ToString () + ((int) IOProtocol.Open).ToString () + _absolutePath) {
                    //打开状态
                    if (_state != IOState.CONNECTED) {
                        _state = IOState.CONNECTED;
                        invoke (IOEvent.CONNECT);
                    }
                } else if (receive == ((int) IOProtocol.Message).ToString () + ((int) IOProtocol.Close).ToString () + _absolutePath) {
                    //关闭状态
                    if (_state != IOState.CLOSED) {
                        _state = IOState.CLOSED;
                        invoke (IOEvent.CLOSE);
                    }
                } else if (receive == ((int) IOProtocol.Pong).ToString ()) {
                    // Pong
                    invoke (IOEvent.PONG);
                } else if (receive.StartsWith (((int) IOProtocol.Message).ToString () + ((int) IOProtocol.Ping).ToString () + _absolutePath)) {
                    // 消息 42 _absolutePath packetid ["event","result","result"...]
                    string message = receive.Substring ((((int) IOProtocol.Message).ToString () + ((int) IOProtocol.Ping).ToString () + _absolutePath).Length);
                    Regex regex = new Regex ($@"^(\d*)\[""([*\s\w-]+)"",([\s\S]*)\]$");
                    if (regex.IsMatch (message)) {
                        var groups = regex.Match (message).Groups;

                        int packetId = -1;
                        if (groups[1].Value != "") {
                            packetId = int.Parse (groups[1].Value);
                        }

                        string eventName = groups[2].Value;
                        object[] results = JsonConvert.DeserializeObject<object[]> ($"[{groups[3].Value}]");
                        string result = JsonConvert.SerializeObject (results[0]);

                        if (result.StartsWith ("\"")) {
                            result = JsonConvert.DeserializeObject<string> (result);
                        }

                        invoke (eventName, result, delegate (object obj) {
                            if (packetId >= 0) {
                                string callback = buildCallback (packetId, obj).Result;
                                send (callback).Wait ();
                            }
                        });
                    }
                } else if (receive.StartsWith (((int) IOProtocol.Message).ToString () + ((int) IOProtocol.Pong).ToString () + _absolutePath)) {
                    // ack消息 43 _absolutePath packetid ["result","result"...]

                    string message = receive.Substring ((((int) IOProtocol.Message).ToString () + ((int) IOProtocol.Pong).ToString () + _absolutePath).Length);

                    Regex regex = new Regex ($@"^(\d+)\[({{[\s\S]*}})\]$");

                    if (regex.IsMatch (message)) {
                        var groups = regex.Match (message).Groups;
                        int packetId = int.Parse (groups[1].Value);
                        if (_callbackEventHandlers.ContainsKey (packetId)) {
                            GeneralEventHandlers generalEventHandlers = _callbackEventHandlers[packetId];

                            object[] results = JsonConvert.DeserializeObject<object[]> ($"[{groups[2].Value}]");
                            string result = JsonConvert.SerializeObject (results[0]);

                            if (result.StartsWith ("\"")) {
                                result = JsonConvert.DeserializeObject<string> (result);
                            }

                            generalEventHandlers (result, delegate (object obj) {
                                throw new Exception ("callback can't call callback");
                            });

                            _callbackEventHandlers.Remove (packetId);
                        }
                    }
                }
            }, _tokenSource.Token);
        }
    }
}
