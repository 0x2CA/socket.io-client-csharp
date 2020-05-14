using System.Collections.Generic;

namespace SocketIO.SocketEvent {
    class EventTarget {

        // 监听事件
        private Dictionary<string, GeneralEventHandler> _GeneralEventHandlers;

        // 监听内置事件
        private Dictionary<SocketStockEvent, StockEventHandler> _StockEventHandlers;

        //服务器执行事件
        private Dictionary<int, ClientCallbackEventHandler> _ClientCallbackEventHandlers;

        public EventTarget () {
            // 事件字典
            _GeneralEventHandlers = new Dictionary<string, GeneralEventHandler> ();
            _StockEventHandlers = new Dictionary<SocketStockEvent, StockEventHandler> ();
            _ClientCallbackEventHandlers = new Dictionary<int, ClientCallbackEventHandler> ();
        }

        // 添加内置事件监听（可重复监听）
        public void On (SocketStockEvent eventName, StockEventHandler callback) {
            if (_StockEventHandlers.ContainsKey (eventName)) {
                StockEventHandler list = _StockEventHandlers[eventName];
                if (list != null) {
                    list += callback;
                    _StockEventHandlers.Remove (eventName);
                    _StockEventHandlers.Add (eventName, list);
                    return;
                }
            }
            _StockEventHandlers.Remove (eventName);
            _StockEventHandlers.Add (eventName, callback);
        }

        // 添加一般事件监听（可重复监听）
        public void On (string eventName, GeneralEventHandler callback) {
            if (_GeneralEventHandlers.ContainsKey (eventName)) {
                GeneralEventHandler list = _GeneralEventHandlers[eventName];
                if (list != null) {
                    list += callback;
                    _GeneralEventHandlers.Remove (eventName);
                    _GeneralEventHandlers.Add (eventName, list);
                    return;
                }

            }
            _GeneralEventHandlers.Remove (eventName);
            _GeneralEventHandlers.Add (eventName, callback);
        }

        // 远程服务器执行监听
        public void On (int packetID, ClientCallbackEventHandler callback) {
            _ClientCallbackEventHandlers.Remove (packetID);
            _ClientCallbackEventHandlers.Add (packetID, callback);
        }

        // 移除内置事件监听
        public void Off (SocketStockEvent eventName, StockEventHandler callback) {
            if (_StockEventHandlers.ContainsKey (eventName)) {
                StockEventHandler list = _StockEventHandlers[eventName];
                if (list != null) {
                    list -= callback;
                    _StockEventHandlers.Remove (eventName);
                    _StockEventHandlers.Add (eventName, list);
                    return;
                }
                _StockEventHandlers.Remove (eventName);
            }
        }

        // 移除一般事件监听
        public void Off (string eventName, GeneralEventHandler callback) {
            if (_GeneralEventHandlers.ContainsKey (eventName)) {
                GeneralEventHandler list = _GeneralEventHandlers[eventName];
                if (list != null) {
                    list -= callback;
                    _GeneralEventHandlers.Remove (eventName);
                    _GeneralEventHandlers.Add (eventName, list);
                    return;
                }

                _GeneralEventHandlers.Remove (eventName);

            }
        }

        // 移除客户端运程函数事件监听
        public void Off (int packetID) {
            _ClientCallbackEventHandlers.Remove (packetID);
        }

        // 调用内置事件监听函数
        public void Emit (SocketStockEvent eventName) {
            if (_StockEventHandlers.ContainsKey (eventName)) {
                StockEventHandler list = _StockEventHandlers[eventName];
                if (list != null) {
                    list.Invoke ();
                } else {
                    _StockEventHandlers.Remove (eventName);
                }
            }
        }

        // 调用一般事件监听函数
        public void Emit (string eventName, string result, ServerCallbackEventHandler callback) {
            if (_GeneralEventHandlers.ContainsKey (eventName)) {
                GeneralEventHandler list = _GeneralEventHandlers[eventName];
                if (list != null) {
                    list.Invoke (result, callback);
                } else {
                    _GeneralEventHandlers.Remove (eventName);
                }
            }
        }

        // 调用客户端运程函数事件监听

        public void Emit (int packetID, string result) {
            if (_ClientCallbackEventHandlers.ContainsKey (packetID)) {
                ClientCallbackEventHandler list = _ClientCallbackEventHandlers[packetID];
                if (list != null) {
                    list.Invoke (result);
                } else {
                    _ClientCallbackEventHandlers.Remove (packetID);
                }
            }
        }

    }
}