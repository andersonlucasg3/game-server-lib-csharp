using System;
using System.Net;
using System.Net.Sockets;
using GameNetworking.Commons;
using Logging;

namespace GameNetworking.Networking.Sockets {
    public interface IUdpSocket<TDerived> : ISocket<TDerived>
        where TDerived : IUdpSocket<TDerived> {
        void Receive();
        void Send(byte[] bytes, int count, NetEndPoint to);
    }

    public interface IUdpSocketIOListener {
        void SocketDidReceiveBytes(UdpSocket socket, byte[] bytes, int count, NetEndPoint from);
        void SocketDidWriteBytes(UdpSocket socket, int count, NetEndPoint to);
    }

    public sealed class UdpSocket : IUdpSocket<UdpSocket> {
        private const int SIO_UDP_CONNRESET = -1744830452;

        private readonly ObjectPool<byte[]> bufferPool;
        private readonly ObjectPool<IPEndPoint> ipEndPointPool;
        private readonly object receiveLock = new object();
        private readonly object sendLock = new object();
        private Socket socket;

        private bool isClosed = false;

        public bool isBound => this.socket.IsBound;
        public bool isConnected { get; private set; }

        public NetEndPoint localEndPoint { get; private set; } = new NetEndPoint();
        public NetEndPoint remoteEndPoint { get; private set; } = new NetEndPoint();

        public IUdpSocketIOListener listener { get; set; }

        public UdpSocket() {
            this.bufferPool = new ObjectPool<byte[]>(() => new byte[Consts.bufferSize]);
            this.ipEndPointPool = new ObjectPool<IPEndPoint>(() => new IPEndPoint(IPAddress.Any, 0));
        }

        public UdpSocket(UdpSocket socket, NetEndPoint remoteEndPoint) : this() {
            this.socket = socket.socket;
            this.remoteEndPoint = remoteEndPoint;
        }

        private void Bind(IPEndPoint endPoint) {
            this.socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp) {
                ReceiveBufferSize = Consts.bufferSize,
                SendBufferSize = Consts.bufferSize,
                Blocking = false
            };
            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            try { this.socket.DontFragment = true; } catch (Exception) { Logger.Log("DontFragment not supported."); }
            try { this.socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null); } catch (Exception) { Logger.Log("Error setting SIO_UDP_CONNRESET. Maybe not running on Windows."); }
            this.socket.Bind(endPoint);

            this.isClosed = false;

            Logger.Log($"Listening on {endPoint}");
        }

        public void Bind(NetEndPoint endPoint) {
            this.localEndPoint = endPoint;
            this.isConnected = true;

            var ipep = this.ipEndPointPool.Rent();
            ipep.Address = endPoint.address;
            ipep.Port = endPoint.port;
            this.Bind(ipep);
            this.ipEndPointPool.Pay(ipep);
        }

        public void Connect(NetEndPoint endPoint) => this.remoteEndPoint = endPoint;

        public void Close() {
            lock (this.sendLock) {
                lock (this.receiveLock) {
                    if (this.isClosed || this.socket == null) { return; }
                    this.socket.Close();
                    this.socket = null;
                    this.isClosed = true;
                }
            }
        }

        public override string ToString() {
            return $"{{EndPoint-{this.remoteEndPoint}}}";
        }

        #region Read & Write

        public void Receive() {
            lock (this.receiveLock) {
                if (this.socket == null) { return; }
                if (this.socket.Poll(1, SelectMode.SelectRead)) {
                    var buffer = this.bufferPool.Rent();
                    var endPoint = this.ipEndPointPool.Rent();
                    endPoint.Address = IPAddress.Any;
                    endPoint.Port = 0;
                    EndPoint ep = endPoint;

                    var count = this.socket.ReceiveFrom(buffer, 0, Consts.bufferSize, SocketFlags.None, ref ep);
                    if (count > 0) { this.listener?.SocketDidReceiveBytes(this, buffer, count, this.From(ep)); }

                    this.bufferPool.Pay(buffer);
                    this.ipEndPointPool.Pay(endPoint);
                }
            }
        }

        public void Send(byte[] bytes, int count, NetEndPoint to) {
            lock (this.sendLock) {
                if (this.socket == null) { return; }
                if (this.socket.Poll(1, SelectMode.SelectWrite)) {
                    IPEndPoint endPoint = this.ipEndPointPool.Rent();
                    this.From(to, ref endPoint);
                    var written = this.socket.SendTo(bytes, 0, count, SocketFlags.None, endPoint);
                    if (written > 0) { this.listener?.SocketDidWriteBytes(this, written, to); }
                    this.ipEndPointPool.Pay(endPoint);
                }
            }
        }

        #endregion

        #region Equatable Methods

        private void From(NetEndPoint ep, ref IPEndPoint endPoint) {
            endPoint.Address = ep.address;
            endPoint.Port = ep.port;
        }

        private NetEndPoint From(EndPoint ep) {
            IPEndPoint endPoint = (IPEndPoint)ep;
            return new NetEndPoint(endPoint.Address, endPoint.Port);
        }

        public bool Equals(IPEndPoint endPoint) => this.remoteEndPoint.Equals(endPoint);

        public override bool Equals(object obj) {
            if (obj is UdpSocket other) {
                return this.Equals(other.remoteEndPoint);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode() => this.remoteEndPoint.GetHashCode();

        #endregion
    }
}