﻿using System.Collections.Generic;
using System.Threading;
using GameNetworking;
using GameNetworking.Networking;
using GameNetworking.Networking.Models;
using Logging;
using Messages.Models;
using Networking.Models;
using Networking.Sockets;
using NUnit.Framework;
using Test.Core.Model;

using UnreliableClientPlayer = GameNetworking.Commons.Models.Client.NetworkPlayer<Networking.Sockets.IUDPSocket, GameNetworking.Networking.Models.UnreliableNetworkClient, Networking.Models.UnreliableNetClient>;
using UnreliableServerPlayer = GameNetworking.Commons.Models.Server.NetworkPlayer<Networking.Sockets.IUDPSocket, GameNetworking.Networking.Models.UnreliableNetworkClient, Networking.Models.UnreliableNetClient>;

namespace Tests.Core {
    public class UnreliableGameServerClientTests : GameServerClientTests<
            UnreliableNetworkingServer, UnreliableNetworkingClient, UnreliableGameServer<UnreliableServerPlayer>, UnreliableGameClient<UnreliableClientPlayer>,
            UnreliableServerPlayer, UnreliableClientPlayer, IUDPSocket, UnreliableNetworkClient, UnreliableNetClient, UnreliableClientAcceptor<UnreliableServerPlayer>,
            UnreliableGameServerClientTests.ServerListener, UnreliableGameServerClientTests.ClientListener
        > {

        private static int ipCounter = 0;

        protected override UnreliableNetworkingClient NewClient() => new UnreliableNetworkingClient(new UnreliableSocket(new UnreliableSocketMock()));
        protected override UnreliableNetworkingServer NewServer() => new UnreliableNetworkingServer(new UnreliableSocket(new UnreliableSocketMock()));

        protected override void NewServer(out UnreliableGameServer<UnreliableServerPlayer> server, out ServerListener listener) {
            var newListener = new ServerListener();
            server = new UnreliableGameServer<UnreliableServerPlayer>(this.NewServer(), new MainThreadDispatcher()) { listener = newListener };
            listener = newListener;
        }

        protected override void NewClient(out UnreliableGameClient<UnreliableClientPlayer> client, out ClientListener listener) {
            var newListener = new ClientListener();
            client = new UnreliableGameClient<UnreliableClientPlayer>(this.NewClient(), new MainThreadDispatcher()) { listener = newListener };
            client.Start($"192.168.0.{ipCounter}", 5);
            ipCounter++;
            listener = newListener;
        }

        [Test]
        public void TestRealSocketConnection() {
            Logger.IsLoggingEnabled = true;

            var mainThreadDispatcher = new MainThreadDispatcher();

            ServerListener serverListener = new ServerListener();
            ClientListener clientListener = new ClientListener();

            var server = new UnreliableGameServer<UnreliableServerPlayer>(new UnreliableNetworkingServer(new UnreliableSocket(new UDPSocket())), mainThreadDispatcher) { listener = serverListener };
            var client = new UnreliableGameClient<UnreliableClientPlayer>(new UnreliableNetworkingClient(new UnreliableSocket(new UDPSocket())), mainThreadDispatcher) { listener = clientListener };

            void Update() {
                this.Update(server);
                this.Update(server);
                this.Update(client);
                this.Update(client);
            }

            var localIP = "127.0.0.1";

            server.Start(localIP, 64000);

            client.Start(localIP, 63000);
            client.Connect(localIP, 64000);

            Update();
            Update();

            Assert.AreEqual(1, serverListener.connectedPlayers.Count);
            Assert.IsTrue(clientListener.connectedCalled);

            void ValidateProcessTiming() {
                Update();
                Update();

                var pingValue = server.pingController.GetPingValue(client.FindPlayer(p => p.isLocalPlayer));
                Logger.Log($"Current ping value: {pingValue}");
                Assert.Less(pingValue, 1);
            }

            var sleepMillis = 10;
            var loopCount = 1000;
            Logger.Log($"Will take {sleepMillis * loopCount / 1000} seconds to finish.");
            for (int index = 0; index < loopCount; index++) {
                Thread.Sleep(sleepMillis);

                ValidateProcessTiming();

                Logger.Log($"Current at index: {index}");
            }
        }

        public class ClientListener : IClientListener<UnreliableClientPlayer, IUDPSocket, UnreliableNetworkClient, UnreliableNetClient> {
            public List<MessageContainer> receivedMessages { get; } = new List<MessageContainer>();
            public List<UnreliableClientPlayer> disconnectedPlayers { get; } = new List<UnreliableClientPlayer>();
            public bool connectedCalled { get; private set; }
            public bool connectTimeoutCalled { get; private set; }
            public bool disconnectCalled { get; private set; }
            public UnreliableClientPlayer localPlayer { get; private set; }

            #region IGameClientListener

            public void GameClientDidConnect() => this.connectedCalled = true;
            public void GameClientConnectDidTimeout() => this.connectTimeoutCalled = true;
            public void GameClientDidDisconnect() => this.disconnectCalled = true;
            public void GameClientDidIdentifyLocalPlayer(UnreliableClientPlayer player) => this.localPlayer = player;
            public void GameClientDidReceiveMessage(MessageContainer container) => this.receivedMessages.Add(container);
            public void GameClientNetworkPlayerDidDisconnect(UnreliableClientPlayer player) => this.disconnectedPlayers.Add(player);

            #endregion
        }

        public class ServerListener : IServerListener<UnreliableServerPlayer, IUDPSocket, UnreliableNetworkClient, UnreliableNetClient> {
            public List<UnreliableServerPlayer> connectedPlayers { get; } = new List<UnreliableServerPlayer>();
            public List<UnreliableServerPlayer> disconnectedPlayers { get; } = new List<UnreliableServerPlayer>();

            #region IGameServerListener

            public void GameServerPlayerDidConnect(UnreliableServerPlayer player) => connectedPlayers.Add(player);
            public void GameServerPlayerDidDisconnect(UnreliableServerPlayer player) => disconnectedPlayers.Add(player);
            public void GameServerDidReceiveClientMessage(MessageContainer container, UnreliableServerPlayer player) => Assert.NotNull(player);

            #endregion
        }
    }
}