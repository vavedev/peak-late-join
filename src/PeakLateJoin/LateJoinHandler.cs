using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakLateJoin
{
    public class LateJoinHandler : MonoBehaviourPunCallbacks
    {
        private ManualLogSource _logger;
        private readonly Dictionary<int, bool> _lastKnownDead = new();

        public void InitLogger(ManualLogSource logger)
        {
            _logger = logger;
        }

        private void Update()
        {
            foreach (Character c in Character.AllCharacters)
            {
                if (c?.photonView == null || c.photonView.Owner == null || c.data == null)
                    continue;

                int actorId = c.photonView.Owner.ActorNumber;
                bool isDead = c.data.dead;

                if (!_lastKnownDead.TryGetValue(actorId, out bool lastState) || lastState != isDead)
                {
                    SaveDeathState(c.photonView.Owner, isDead);
                    _lastKnownDead[actorId] = isDead;

                    _logger?.LogInfo($"[DeathSync] {c.characterName}: {(isDead ? "dead" : "alive")}");
                }
            }
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != "Airport")
            {
                StartCoroutine(HandleLateJoin(newPlayer));
            }
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            Character character = PlayerHandler.GetPlayerCharacter(otherPlayer);
            if (character != null && character.data != null)
            {
                SaveDeathState(otherPlayer, character.data.dead);
                _lastKnownDead[otherPlayer.ActorNumber] = character.data.dead;
            }
        }

        private IEnumerator HandleLateJoin(Photon.Realtime.Player newPlayer)
        {
            Character newCharacter = null;

            // Wait until Character is initialized
            while (newCharacter == null)
            {
                newCharacter = PlayerHandler.GetPlayerCharacter(newPlayer);
                yield return null;
            }

            // Wait until CharacterData exists
            while (newCharacter.data == null)
                yield return null;

            // Wait a bit longer to avoid conflict with RPC_SyncOnJoin
            yield return new WaitForSeconds(1.5f);

            bool wasDead = false;
            string savedStage = null;

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue($"dead_{newPlayer.ActorNumber}", out object deadState))
            {
                wasDead = (bool)deadState;
            }
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue($"stage_{newPlayer.ActorNumber}", out object stageState))
            {
                savedStage = stageState as string;
            }

            string currentStage = SceneManager.GetActiveScene().name;

            _logger?.LogInfo($"[LateJoin] {newCharacter.characterName} joined. SavedStage={savedStage}, CurrentStage={currentStage}, WasDead={wasDead}");

            // Ensure they spawn after RPC sync has settled
            yield return new WaitForSeconds(0.5f);

            ImprovedSpawnTarget spawnTarget = PopulateSpawnData(newCharacter);

            if (spawnTarget.LowestCharacter == null)
            {
                _logger.LogWarning($"[LateJoin] No valid spawn target found. Keeping {newCharacter.characterName} at spawn.");
                yield break;
            }

            Vector3 targetPos = spawnTarget.LowestCharacter.data.isGrounded
                ? spawnTarget.LowestCharacter.data.groundPos
                : spawnTarget.LowestCharacter.transform.position;

            _logger.LogInfo($"[LateJoin] {newCharacter.characterName} → warping near {spawnTarget.LowestCharacter.characterName} at Y={targetPos.y:F2}");

            // Try updating spawnPoint before teleport (engine may override position)
            if (newCharacter.data.spawnPoint != null)
            {
                newCharacter.data.spawnPoint.position = targetPos;
            }

            // Warp to lowest player
            yield return TeleportUtils.SafeWarp(newCharacter, spawnTarget.LowestCharacter, _logger);

            // Handle death state restoration
            if (wasDead)
            {
                if (savedStage == currentStage)
                {
                    newCharacter.data.dead = true;
                    _logger.LogInfo($"[LateJoin] {newCharacter.characterName} kept dead (same stage).");
                }
                else
                {
                    newCharacter.data.dead = false;
                    _logger.LogInfo($"[LateJoin] {newCharacter.characterName} revived (stage changed).");
                }
            }

            SaveDeathState(newPlayer, newCharacter.data.dead);
            _lastKnownDead[newPlayer.ActorNumber] = newCharacter.data.dead;

            _logger.LogInfo($"[LateJoin] Finished processing {newCharacter.characterName}.");
        }

        private static ImprovedSpawnTarget PopulateSpawnData(Character newCharacter)
        {
            ImprovedSpawnTarget result = default;

            foreach (Character c in Character.AllCharacters)
            {
                if (c == null || c == newCharacter || c.data == null || c.data.dead)
                    continue;

                float heightY = c.data.isGrounded ? c.data.groundPos.y : c.transform.position.y;
                result.RegisterCharacter(c, heightY);
            }

            return result;
        }

        private static void SaveDeathState(Photon.Realtime.Player player, bool isDead)
        {
            var currentScene = SceneManager.GetActiveScene().name;

            var props = new ExitGames.Client.Photon.Hashtable
            {
                { $"dead_{player.ActorNumber}", isDead },
                { $"stage_{player.ActorNumber}", currentScene }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }
}
