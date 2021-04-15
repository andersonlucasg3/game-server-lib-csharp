using System;
using System.Threading;
using GameNetworking.Commons;
using GameNetworking.Messages.Models;
using GameNetworking.Messages.Streams;
using GameNetworking.Networking.Sockets;
using Logging;

namespace GameNetworking.Channels
{
    public class ReliableChannel : Channel<ReliableChannel>, ITcpSocketIOListener
    {
        private static readonly PooledList<ReliableChannel> _aliveSockets = PooledList<ReliableChannel>.Rent();
        private static readonly object _socketLock = new object();
        
        private static bool _ioRunning;

        private readonly MessageStreamReader _reader;
        private readonly ITcpSocket _socket;
        private readonly MessageStreamWriter _writer;
        
        private Action<byte[], int> _channelSend;

        public ReliableChannel(ITcpSocket socket)
        {
            _socket = socket;
            _socket.ioListener = this;

            _reader = new MessageStreamReader();
            _writer = new MessageStreamWriter();

            _channelSend = _socket.Send;
        }

        ~ReliableChannel()
        {
            lock(_socketLock) _aliveSockets.Dispose();
        }

        void ITcpSocketIOListener.SocketDidReceiveBytes(ITcpSocket remoteSocket, byte[] bytes, int count)
        {
            _reader.Add(bytes, count);

            ReadAllMessages(_reader, _socket.remoteEndPoint);
        }

        void ITcpSocketIOListener.SocketDidSendBytes(ITcpSocket remoteSocket, int count)
        {
            _writer.DidWrite(count);
        }

        public static void StartIO()
        {
            _ioRunning = true;
            ThreadPool.QueueUserWorkItem(ThreadPoolWorker);
        }

        public static void StopIO()
        {
            _ioRunning = false;
        }

        public static void Add(ReliableChannel channel)
        {
            lock (_socketLock) _aliveSockets.Add(channel);
        }

        public static void Remove(ReliableChannel channel)
        {
            lock (_socketLock) _aliveSockets.Remove(channel);
        }

        public void CloseChannel()
        {
            ThreadChecker.AssertMainThread();

            _socket.Disconnect();
        }

        public void Send(ITypedMessage message)
        {
            _writer.Write(message);
        }

        private static void ThreadPoolWorker(object state)
        {
            Thread.CurrentThread.Name = "ReliableChannel Thread";
            ThreadChecker.ConfigureReliable(Thread.CurrentThread);
            var channels = new ReliableChannel[100];
            do
            {
                int channelCount;
                lock (_socketLock)
                {
                    _aliveSockets.CopyTo(channels);
                    channelCount = _aliveSockets.Count;
                }

                for (var index = 0; index < channelCount; index++)
                {
                    ReliableChannel channel = channels[index];
                    try
                    {
                        channel._socket.Receive();
                        channel._writer.Use(channel._channelSend);
                    }
                    catch (ObjectDisposedException)
                    {
                        _ioRunning = false;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Exception thrown in ThreadPool\n{ex}");
                    }
                }
            } while (_ioRunning);

            Logger.Log("ReliableChannel ThreadPool EXITING");
            ThreadChecker.ConfigureReliable(null);
        }
    }
}
