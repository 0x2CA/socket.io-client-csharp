using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SocketIO.SocketEvent;
using SocketIO.SocketRequest;
using SocketIO.SocketRequest.Mode;
using SocketIO.SocketResponse;

namespace SocketIO {

    public class IO {

        public IO (Uri url) {
            //协议判断
            if (url.Scheme == "https" || url.Scheme == "http" || url.Scheme == "wss" || url.Scheme == "ws") {
                _url = url;
            } else {
                throw new ArgumentException ("Unsupported protocol");
            }

            _eventTarget = new EventTarget ();
        }

        public IO (string url) : this (new Uri (url)) { }

        // 连接地址
        private Uri _url;

        //绝对地址
        private string _absolutePath = "";

        // 连接附加参数
        private Dictionary<string, string> _parameters;

        // 请求封装对象
        private Request _request;

        // 响应封装对象
        private Response _response;

        // 事件中心
        private EventTarget _eventTarget;

        // socket 连接
        private ClientWebSocket _socket;

        // 取消令牌
        private CancellationTokenSource _tokenSource;

        //连接状态
        public SocketState getState () {
            if (_response != null) {
                return _response.state;

            } else {
                return SocketState.Close;
            }
        }

        // 发送消息注册函数
        public Task emit (string eventName, object obj, ClientCallbackEventHandler callback) {

            return Task.Run (() => {
                if (getState () == SocketState.Connect) {
                    _request.emit (eventName, obj, callback).Wait ();
                }
            }, _tokenSource.Token);

        }

        // 发送消息
        public Task emit (string eventName, object obj) {
            return Task.Run (() => {
                if (getState () == SocketState.Connect) {
                    _request.emit (eventName, obj).Wait ();
                }
            }, _tokenSource.Token);

        }

        public void on (string eventName, GeneralEventHandler callback) {
            _eventTarget.on (eventName, callback);
        }

        public void on (SocketStockEvent eventName, StockEventHandler callback) {
            _eventTarget.on (eventName, callback);

        }

        public void off (string eventName, GeneralEventHandler callback) {
            _eventTarget.off (eventName, callback);
        }

        public void off (SocketStockEvent eventName, StockEventHandler callback) {
            _eventTarget.off (eventName, callback);
        }

        // 发起连接
        public Task connect () {
            if (_socket == null || (_socket.State == WebSocketState.Closed && _socket.State == WebSocketState.Closed)) {
                // 取消令牌生成
                _tokenSource = new CancellationTokenSource ();

                // socket 连接对象
                _socket = new ClientWebSocket ();

                //请求对象
                _request = new Request (_socket, _url, _eventTarget, _tokenSource);
                //响应对象
                _response = new Response (_socket, _url, _eventTarget, _tokenSource, _request);

                _request.connect ().Wait ();

                _response.listen ();

            }
            return Task.CompletedTask;

        }

        // 关闭连接
        public Task close () {
            if (_response.state == SocketState.Connect) {
                if (_tokenSource != null) {
                    _tokenSource.Cancel ();
                    _tokenSource.Dispose ();
                }

                _tokenSource = null;

                if (_response.state != SocketState.Close) {
                    _response.state = SocketState.Close;
                    _eventTarget.invoke (SocketStockEvent.Close);
                }
                if (_socket != null) {
                    _socket.Abort ();
                    _socket.Dispose ();
                }
                _socket = null;

                _request = null;
                _response = null;
            }
            return Task.CompletedTask;
        }
    }
}