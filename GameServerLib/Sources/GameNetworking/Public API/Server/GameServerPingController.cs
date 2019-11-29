using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameNetworking {
    using Models;
    using Models.Server;
    using Messages.Server;
    using Commons;

    public class GameServerPingController : BaseWorker<GameServer>, INetworkPlayerStorageChangeDelegate {
        private readonly Dictionary<int, PingPlayer> pingPlayers = new Dictionary<int, PingPlayer>();

        public float PingInterval { get; set; }

        public GameServerPingController(GameServer instance, NetworkPlayersStorage storage) : base(instance) {
            storage.Add(this);
        }

        public float GetPingValue(NetworkPlayer player) {
            return this.pingPlayers[player.playerId].PingValue;
        }

        internal void Update() {
            foreach (var pingPlayer in this.pingPlayers.Values) {
                if (!pingPlayer.PingSent && pingPlayer.CanSendNextPing) {
                    pingPlayer.SendingPing();
                    this.instance.Send(new PingRequestMessage(), pingPlayer.player.client);
                }
            }
        }

        public float PongReceived(NetworkPlayer player) {
            var pingPlayer = this.pingPlayers[player.playerId];
            var pingValue = pingPlayer?.ReceivedPong() ?? 0F;
            player.mostRecentPingValue = pingValue;
            return pingValue;
        }

        void INetworkPlayerStorageChangeDelegate.PlayerStorageDidAdd(NetworkPlayer player) {
            this.pingPlayers[player.playerId] = new PingPlayer(player);
        }

        void INetworkPlayerStorageChangeDelegate.PlayerStorageDidRemove(NetworkPlayer player) {
            if (this.pingPlayers.ContainsKey(player.playerId)) {
                this.pingPlayers.Remove(player.playerId);
            }
        }
    }

    internal class PingPlayer : WeakReference {
        private WeakReference pingController;

        private float pingSentTime;

        private float PingElapsedTime { get { return Time.time - this.pingSentTime; } }

        internal bool PingSent { get; private set; }
        internal bool CanSendNextPing { get { return this.PingElapsedTime > (PingController?.PingInterval ?? 0.5F); } }
        internal float PingValue { get; private set; }

        internal NetworkPlayer player { get { return this.Target as NetworkPlayer; } }

        internal GameServerPingController PingController {
            get { return pingController?.Target as GameServerPingController; }
            set {
                if (value == null) { pingController = null; return; }
                pingController = new WeakReference(value);
            }
        }

        internal PingPlayer(NetworkPlayer instance) : base(instance) { }

        internal void SendingPing() {
            this.PingSent = true;
            this.pingSentTime = Time.time;
        }

        internal float ReceivedPong() {
            this.PingSent = false;
            this.PingValue = this.PingElapsedTime;
            return this.PingValue;
        }

        public override bool Equals(object obj) {
            if (obj is NetworkPlayer) {
                return this.player == (NetworkPlayer)obj;
            }
            return Equals(this, obj);
        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }
    }
}
