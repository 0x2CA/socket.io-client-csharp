# socket.io-client-csharp
 Socket.IO Client CSharp
This is the Socket.IO client for .NET, which is base on ClientWebSocket, provide a simple way to connect to the Socket.IO server. The target framework is .NET Standard 2.0
## Usage
```C#
    IO socket = new IO (url);
    socket.on (SocketStockEvent.Close, () => {
        Console.WriteLine ("Socket Close");
    });

    socket.on (SocketStockEvent.Ping, () => {
        Console.WriteLine ("Socket Ping");
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
        socket.emit ("test", "123456", (result) => {
            // server can run fun
            
           // Next, you might parse the data in this way.
           var obj = JsonConvert.DeserializeObject<T> (result);
           // Or, read some fields
           var jobj = JObject.Parse (result);
           int code = jobj.Value<int> ("code");
           bool hasMore = jobj["data"].Value<bool> ("hasMore");
           var data = jobj["data"].ToObject<ResponseData> ();
            // ...
        });
    });

    socket.connect ();

    socket.on ("test", (result, callback) => {

        // Next, you might parse the data in this way.
        var obj = JsonConvert.DeserializeObject<T> (result);
        // Or, read some fields
        var jobj = JObject.Parse (result);
        int code = jobj.Value<int> ("code");
        bool hasMore = jobj["data"].Value<bool> ("hasMore");
        var data = jobj["data"].ToObject<ResponseData> ();
        // ...

        // can run server fun
        callback ("hello");
    });

```

## Stock Event

```C#
 public enum IOEvent
    {
        OPEN = 0,
        CLOSE = 1,
        CONNECT = 2,
        PING = 3,
        PONG = 4,
        ABORTED = 5
    }
```
