using System;

namespace SocketIO.Test {
    class Program {
        static void Main (string[] args) {
            IO socket = new IO ("wss://xcx.test.tongchuanggame.com:2020");
            socket.on (SocketStockEvent.Close, () => {
                Console.WriteLine ("Socket Close");
            });

            int count = 0;
            socket.on (SocketStockEvent.Ping, () => {
                Console.WriteLine ("Socket Ping");
                count++;

                if (count > 1) {
                    socket.close ().Wait ();
                    count = 0;
                }
                socket.connect ().Wait ();

            });

            socket.on (SocketStockEvent.Pong, () => {
                Console.WriteLine ("Socket Pong");
            });

            socket.on (SocketStockEvent.Abort, () => {
                Console.WriteLine ("Socket Abort");
            });

            socket.on (SocketStockEvent.Open, () => {
                Console.WriteLine ("Socket Open");
            });

            socket.on (SocketStockEvent.Connect, () => {
                Console.WriteLine ("Socket Connect");

                socket.emit ("test", "12345", (result) => {
                    Console.WriteLine ("server run:" + result);
                });
            });

            socket.connect ().Wait ();

            socket.on ("test", (result, callback) => {
                Console.WriteLine ("Get:" +
                    result);
                callback ("hello");

            });

            while (true) {

            }
        }
    }
}