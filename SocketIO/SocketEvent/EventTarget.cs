using System;
using System.Collections.Generic;
using System.Text;

namespace SocketIO.SocketEvent {
    class EventTarget {

        // 监听事件
        private Dictionary<string, GeneralEventHandler> _generalEventHandlers;

        // 监听内置事件
        private Dictionary<SocketStockEvent, StockEventHandler> _stockEventHandlers;

        //服务器执行事件
        private Dictionary<int, ClientCallbackEventHandler> _clientCallbackEventHandlers;

        public EventTarget () {
            // 事件字典
            _generalEventHandlers = new Dictionary<string, GeneralEventHandler> ();
            _stockEventHandlers = new Dictionary<SocketStockEvent, StockEventHandler> ();
            _clientCallbackEventHandlers = new Dictionary<int, ClientCallbackEventHandler> ();
        }

        // 添加内置事件监听（可重复监听）
        public void on (SocketStockEvent eventName, StockEventHandler callback) {
            if (_stockEventHandlers.ContainsKey (eventName)) {
                StockEventHandler list = _stockEventHandlers[eventName];
                if (list != null) {
                    list += callback;
                    _stockEventHandlers.Remove (eventName);
                    _stockEventHandlers.Add (eventName, list);
                    return;
                }
            }
            _stockEventHandlers.Remove (eventName);
            _stockEventHandlers.Add (eventName, callback);
        }

        // 添加一般事件监听（可重复监听）
        public void on (string eventName, GeneralEventHandler callback) {
            if (_generalEventHandlers.ContainsKey (eventName)) {
                GeneralEventHandler list = _generalEventHandlers[eventName];
                if (list != null) {
                    list += callback;
                    _generalEventHandlers.Remove (eventName);
                    _generalEventHandlers.Add (eventName, list);
                    return;
                }

            }
            _generalEventHandlers.Remove (eventName);
            _generalEventHandlers.Add (eventName, callback);
        }

        // 远程服务器执行监听
        public void on (int packetID, ClientCallbackEventHandler callback) {
            _clientCallbackEventHandlers.Remove (packetID);
            _clientCallbackEventHandlers.Add (packetID, callback);
        }

        // 移除内置事件监听
        public void off (SocketStockEvent eventName, StockEventHandler callback) {
            if (_stockEventHandlers.ContainsKey (eventName)) {
                StockEventHandler list = _stockEventHandlers[eventName];
                if (list != null) {
                    list -= callback;
                    _stockEventHandlers.Remove (eventName);
                    _stockEventHandlers.Add (eventName, list);
                    return;
                }
                _stockEventHandlers.Remove (eventName);
            }
        }

        // 移除一般事件监听
        public void off (string eventName, GeneralEventHandler callback) {
            if (_generalEventHandlers.ContainsKey (eventName)) {
                GeneralEventHandler list = _generalEventHandlers[eventName];
                if (list != null) {
                    list -= callback;
                    _generalEventHandlers.Remove (eventName);
                    _generalEventHandlers.Add (eventName, list);
                    return;
                }

                _generalEventHandlers.Remove (eventName);

            }
        }

        // 移除客户端运程函数事件监听
        public void off (int packetID) {
            _clientCallbackEventHandlers.Remove (packetID);
        }

        // 调用内置事件监听函数
        public void invoke (SocketStockEvent eventName) {
            if (_stockEventHandlers.ContainsKey (eventName)) {
                StockEventHandler list = _stockEventHandlers[eventName];
                if (list != null) {
                    list.Invoke ();
                } else {
                    _stockEventHandlers.Remove (eventName);
                }
            }
        }

        // 调用一般事件监听函数
        public void invoke (string eventName, string result, ServerCallbackEventHandler callback) {
            if (_generalEventHandlers.ContainsKey (eventName)) {
                GeneralEventHandler list = _generalEventHandlers[eventName];
                if (list != null) {
                    list.Invoke (result, callback);
                } else {
                    _generalEventHandlers.Remove (eventName);
                }
            }
        }

        // 调用客户端运程函数事件监听

        public void invoke (int packetID, string result) {
            if (_clientCallbackEventHandlers.ContainsKey (packetID)) {
                ClientCallbackEventHandler list = _clientCallbackEventHandlers[packetID];
                if (list != null) {
                    list.Invoke (result);
                } else {
                    _clientCallbackEventHandlers.Remove (packetID);
                }
            }
        }

    }
}