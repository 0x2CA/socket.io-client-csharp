using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using SocketIO.SocketEvent;
using SocketIO.SocketRequest;
using SocketIO.SocketResponse;

namespace SocketIO {

    public class IO {

        public IO (Uri url) {
            //协议判断
            if (url.Scheme == "https" || url.Scheme == "http" || url.Scheme == "wss" || url.Scheme == "ws") {
                _Url = url;
            } else {
                throw new ArgumentException ("Unsupported protocol");
            }

            _EventTarget = new EventTarget ();
        }

        public IO (string url) : this (new Uri (url)) { }

        // 连接地址
        private Uri _Url;

        // 连接附加参数
        private Dictionary<string, string> _Parameters;

        // 请求封装对象
        private Request _Request;

        // 响应封装对象
        private Response _Response;

        // 事件中心
        private EventTarget _EventTarget;

        // socket 连接
        private ClientWebSocket _Socket;

        // 取消令牌
        private CancellationTokenSource _TokenSource;

        //连接状态
        public SocketState GetState () {
            if (_Response != null) {
                return _Response.State;

            } else {
                return SocketState.Close;
            }
        }

        // 发送消息注册函数
        public Task Emit (string eventName, object obj, ClientCallbackEventHandler callback) {

            return Task.Run (() => {
                if (GetState () == SocketState.Connect) {
                    _Request.Emit (eventName, obj, callback).Wait ();
                }
            }, _TokenSource.Token);

        }

        // 发送消息
        public Task Emit (string eventName, object obj) {
            return Task.Run (() => {
                if (GetState () == SocketState.Connect) {
                    _Request.Emit (eventName, obj).Wait ();
                }
            }, _TokenSource.Token);

        }

        public void On (string eventName, GeneralEventHandler callback) {
            _EventTarget.On (eventName, callback);
        }

        public void On (SocketStockEvent eventName, StockEventHandler callback) {
            _EventTarget.On (eventName, callback);

        }

        public void Off (string eventName, GeneralEventHandler callback) {
            _EventTarget.Off (eventName, callback);
        }

        public void Off (SocketStockEvent eventName, StockEventHandler callback) {
            _EventTarget.Off (eventName, callback);
        }

        // 发起连接
        public Task Connect () {
            if (_Socket == null || (_Socket.State == WebSocketState.Closed && _Socket.State == WebSocketState.Closed)) {
                // 取消令牌生成
                _TokenSource = new CancellationTokenSource ();

                // socket 连接对象
                _Socket = new ClientWebSocket ();

                //请求对象
                _Request = new Request (_Socket, _Url, _EventTarget, _TokenSource);
                //响应对象
                _Response = new Response (_Socket, _Url, _EventTarget, _TokenSource, _Request);

                _Request.Connect ().Wait ();

                _Response.Listen ();

            }
            return Task.CompletedTask;

        }

        // 关闭连接
        public Task Close () {
            if (_Response.State == SocketState.Connect) {
                if (_TokenSource != null) {
                    _TokenSource.Cancel ();
                    _TokenSource.Dispose ();
                }

                _TokenSource = null;

                if (_Response.State != SocketState.Close) {
                    _Response.State = SocketState.Close;
                    _EventTarget.Emit (SocketStockEvent.Close);
                }
                if (_Socket != null) {
                    _Socket.Abort ();
                    _Socket.Dispose ();
                }
                _Socket = null;

                _Request = null;
                _Response = null;
            }
            return Task.CompletedTask;
        }
    }
}