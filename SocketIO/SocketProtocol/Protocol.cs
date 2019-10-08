namespace SocketIO.SocketProtocol {

    public enum Protocol {
        Open,
        Close,
        Ping,
        Pong,
        Message,
        Upgrade,
        Noop
    }
}