﻿using GameNetworking.Channels;
using GameNetworking.Commons;
using GameNetworking.Commons.Client;
using GameNetworking.Executors.Client;
using GameNetworking.Messages.Client;
using GameNetworking.Messages.Models;
using GameNetworking.Messages.Server;
using GameNetworking.Networking;
using GameNetworking.Networking.Commons;
using GameNetworking.Networking.Sockets;

namespace GameNetworking.Client {
    public interface IGameClientListener<TPlayer>
        where TPlayer : IPlayer {
        void GameClientDidConnect(Channel channel);
        void GameClientConnectDidTimeout();
        void GameClientDidDisconnect();

        void GameClientDidReceiveMessage(MessageContainer container);
        void GameClientPlayerDidConnect(TPlayer player);
        void GameClientDidIdentifyLocalPlayer(TPlayer player);
        void GameClientPlayerDidDisconnect(TPlayer player);
    }

    public interface IGameClient<TPlayer>
        where TPlayer : Player {
        IReadOnlyPlayerCollection<int, TPlayer> playerCollection { get; }
        TPlayer localPlayer { get; }

        IGameClientListener<TPlayer> listener { get; set; }

        void Connect(string host, int port);
        void Disconnect();

        void Update();

        void Send(ITypedMessage message, Channel channel);
    }

    internal interface IRemoteClientListener {
        void RemoteClientDidConnect(int playerId, bool isLocalPlayer);
        void RemoteClientDidDisconnect(int playerId);
    }

    public class GameClient<TPlayer> : IGameClient<TPlayer>, IRemoteClientListener, INetworkClientListener, IMessageAckHelperListener<NatIdentifierResponseMessage>
        where TPlayer : Player, new() {
        private readonly GameClientMessageRouter<TPlayer> router;
        private readonly PlayerCollection<int, TPlayer> _playerCollection = new PlayerCollection<int, TPlayer>();

        private MessageAckHelper<NatIdentifierRequestMessage, NatIdentifierResponseMessage> natIdentifierAckHelper;

        public NetworkClient networkClient { get; }
        public IReadOnlyPlayerCollection<int, TPlayer> playerCollection => this._playerCollection;
        public TPlayer localPlayer { get; private set; }

        public IGameClientListener<TPlayer> listener { get; set; }

        public GameClient(NetworkClient networkClient, GameClientMessageRouter<TPlayer> router) {
            ThreadChecker.AssertMainThread();

            this.networkClient = networkClient;
            this.networkClient.listener = this;

            this.natIdentifierAckHelper = new MessageAckHelper<NatIdentifierRequestMessage, NatIdentifierResponseMessage>(
                this.networkClient.unreliableChannel, router, 10, 2F
            ) { listener = this };

            this.router = router;
            this.router.Configure(this);
        }

        public void Connect(string host, int port) {
            ThreadChecker.AssertMainThread();
            this.networkClient.Connect(host, port);
        }

        public void Disconnect() {
            ThreadChecker.AssertMainThread();
            this.networkClient.Disconnect();
        }

        public void Send(ITypedMessage message, Channel channel) {
            ThreadChecker.AssertMainThread();
            switch (channel) {
            case Channel.reliable:
                this.networkClient.reliableChannel.Send(message);
                break;
            case Channel.unreliable:
                var remote = this.networkClient.remoteEndPoint;
                this.networkClient.unreliableChannel.Send(message, remote);
                break;
            }
        }

        public virtual void Update() {
            ThreadChecker.AssertMainThread();
            this.natIdentifierAckHelper?.Update();
        }

        void INetworkClientListener.NetworkClientDidConnect() {
            ThreadChecker.AssertReliableChannel();
            this.router.dispatcher.Enqueue(() => this.listener?.GameClientDidConnect(Channel.reliable));
        }

        void INetworkClientListener.NetworkClientConnectDidTimeout() {
            ThreadChecker.AssertReliableChannel();
            this.router.dispatcher.Enqueue(() => this.listener?.GameClientConnectDidTimeout());
        }

        void INetworkClientListener.NetworkClientDidReceiveMessage(MessageContainer container) {
            ThreadChecker.AssertReliableChannel();
            this.router.Route(container);
        }

        void INetworkClientListener.NetworkClientDidReceiveMessage(MessageContainer container, NetEndPoint from) {
            ThreadChecker.AssertUnreliableChannel();
            if (this.natIdentifierAckHelper != null) {
                this.natIdentifierAckHelper.Route(from, container);
            } else {
                this.router.Route(container);
            }
        }

        void INetworkClientListener.NetworkClientDidDisconnect() {
            ThreadChecker.AssertReliableChannel();
            this.router.dispatcher.Enqueue(() => this.listener?.GameClientDidDisconnect());
            this._playerCollection.Clear();
            this.localPlayer = null;
        }

        void IRemoteClientListener.RemoteClientDidConnect(int playerId, bool isLocalPlayer) {
            ThreadChecker.AssertReliableChannel();

            var player = new TPlayer();
            player.Configure(playerId, isLocalPlayer);

            this._playerCollection.Add(playerId, player);

            this.listener?.GameClientPlayerDidConnect(player);
            if (player.isLocalPlayer) {
                this.localPlayer = player;
                this.router.dispatcher.Enqueue(() => this.listener?.GameClientDidIdentifyLocalPlayer(player));

                var endPoint = this.networkClient.localEndPoint;
                var remote = this.networkClient.remoteEndPoint;
                this.natIdentifierAckHelper.Start(new NatIdentifierRequestMessage(player.playerId, endPoint.address, endPoint.port), remote);
            }
        }

        void IRemoteClientListener.RemoteClientDidDisconnect(int playerId) {
            ThreadChecker.AssertReliableChannel();

            var player = this._playerCollection.Remove(playerId);
            this.router.dispatcher.Enqueue(() => this.listener?.GameClientPlayerDidDisconnect(player));
        }

        void IMessageAckHelperListener<NatIdentifierResponseMessage>.MessageAckHelperFailed() {
            ThreadChecker.AssertUnreliableChannel();

            this.Disconnect();
            this.natIdentifierAckHelper = null;
        }

        void IMessageAckHelperListener<NatIdentifierResponseMessage>.MessageAckHelperReceivedExpectedResponse(NetEndPoint from, NatIdentifierResponseMessage message) {
            ThreadChecker.AssertUnreliableChannel();

            this.router.dispatcher.Enqueue(() => new NatIdentifierResponseExecutor<TPlayer>().Execute(this, message));
            this.natIdentifierAckHelper = null;
        }
    }
}