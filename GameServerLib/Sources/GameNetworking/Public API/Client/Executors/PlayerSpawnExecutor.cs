﻿using UnityEngine;

namespace GameNetworking.Executors.Client {
    using Messages.Server;

    internal struct PlayerSpawnExecutor: IExecutor {
        private readonly GameClient gameClient;
        private readonly PlayerSpawnMessage spawnMessage;

        internal PlayerSpawnExecutor(GameClient client, PlayerSpawnMessage message) {
            this.gameClient = client;
            this.spawnMessage = message;
        }

        public void Execute() {
            Logging.Logger.Log(this.GetType(), string.Format("Executing for playerId {0}", this.spawnMessage.playerId));

            var player = this.gameClient.FindPlayer(this.spawnMessage.playerId);

            player.SpawnId = this.spawnMessage.spawnId;

            var spawned = this.gameClient.Delegate?.GameClientSpawnCharacter(player);
            player.GameObject = spawned;

            SetupCharacterControllerIfNeeded(spawned);
        }

        private void SetupCharacterControllerIfNeeded(GameObject spawned) {
            CharacterController charController;
            if (!spawned.TryGetComponent(out charController)) {
                Position(spawned.transform);
                return;
            }

            charController.enabled = false;

            Position(spawned.transform);

            charController.enabled = true;
        }

        private void Position(Transform transform) {
            Vector3 pos = Vector3.zero;
            Vector3 euler = Vector3.zero;
            this.spawnMessage.position.CopyToVector3(ref pos);
            this.spawnMessage.rotation.CopyToVector3(ref euler);
            transform.position = pos;
            transform.eulerAngles = euler;
        }
    }
}