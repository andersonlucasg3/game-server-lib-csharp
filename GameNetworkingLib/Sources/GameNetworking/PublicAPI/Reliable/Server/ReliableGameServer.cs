﻿using GameNetworking.Networking.Commons;
using Networking.Sockets;
using Networking.Models;
using GameNetworking.Networking;
using GameNetworking.Commons.Server;
using GameNetworking.Networking.Models;
using GameNetworking.Commons.Models.Server;
using GameNetworking.Commons;

namespace GameNetworking {
    public class ReliableGameServer<TPlayer> : GameServer<ReliableNetworkingServer, TPlayer, ITCPSocket, ReliableNetworkClient, ReliableNetClient, ReliableClientAcceptor<TPlayer>, ReliableGameServer<TPlayer>>
        where TPlayer : class, INetworkPlayer<ITCPSocket, ReliableNetworkClient, ReliableNetClient>, new() {

        public ReliableGameServer(ReliableNetworkingServer server, IMainThreadDispatcher dispatcher) : base(server, dispatcher) {
            this.networkingServer.listener = this;
        }

        public void Disconnect(TPlayer player) {
            this.networkingServer.Disconnect(player.client);
        }
    }
}
