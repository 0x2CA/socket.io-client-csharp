using System;
using System.Collections.Generic;
using System.Text;

namespace SocketIO.SocketUrl {
    class Url {
        private static int _Eio = 3;

        // 构建完整地址
        public static Uri Build (Uri url, Dictionary<string, string> parameters) {
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
                .Append (_Eio.ToString ())
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
    }
}