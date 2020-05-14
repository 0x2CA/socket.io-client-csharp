using System;

namespace SocketIO.Test {
    class Program {
        static void Main (string[] args) {
            IO socket = new IO ("wss://xcx.test.tongchuanggame.com:2020");
            socket.On (SocketStockEvent.Close, () => {
                Console.WriteLine ("Socket Close");
            });

            int count = 0;
            socket.On(SocketStockEvent.Ping, () => {
                Console.WriteLine ("Socket Ping");
                count++;

                if (count > 1) {
                    socket.Close ().Wait ();
                    count = 0;
                }
                socket.Connect().Wait ();

            });

            socket.On(SocketStockEvent.Pong, () => {
                Console.WriteLine ("Socket Pong");
            });

            socket.On(SocketStockEvent.Abort, () => {
                Console.WriteLine ("Socket Abort");
            });

            socket.On(SocketStockEvent.Open, () => {
                Console.WriteLine ("Socket Open");
            });

            socket.On(SocketStockEvent.Connect, () => {
                Console.WriteLine ("Socket Connect");

                socket.Emit ("test", "12345", (result) => {
                    Console.WriteLine ("server run:" + result);
                });
            });

            socket.Connect ().Wait ();

            socket.On("test", (result, callback) => {
                Console.WriteLine ("Get:" +
                    result);
                callback ("hello");

            });

            while (true) {

            }
        }
    }
}