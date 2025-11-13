using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakLateJoin
{
    /// <summary>
    /// Handles late joins and teleports new players near the lowest living player.
    /// Persists per-player death/stage state in room custom properties so joins can be restored.
    /// </summary>
    public class LateJoinHandler : MonoBehaviourPunCallbacks
    {
        private ManualLogSource _logger = null!;
        private readonly Dictionary<int, bool> _lastKnownDead = new();

        private const float InitialJoinWaitSeconds = 1.5f;
        private const float AfterSyncWaitSeconds = 0.5f;
        private const float CharacterWaitTimeoutSeconds = 10f;

        public void InitLogger(ManualLogSource logger)
        {
            _logger = logger;
        }

        private void Update()
        {
            // Keep a last-known dead state for all characters (so we can persist/propagate)
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
            // Avoid airport scene (per your original logic)
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != "Airport")
            {
                StartCoroutine(HandleLateJoin(newPlayer));
            }
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            if (otherPlayer == null)
                return;

            Character character = PlayerHandler.GetPlayerCharacter(otherPlayer);
            if (character != null && character.data != null)
            {
                SaveDeathState(otherPlayer, character.data.dead);
                _lastKnownDead[otherPlayer.ActorNumber] = character.data.dead;
            }
        }

        private IEnumerator HandleLateJoin(Photon.Realtime.Player newPlayer)
        {
            if (newPlayer == null)
            {
                _logger?.LogWarning("[LateJoin] newPlayer was null.");
                yield break;
            }

            Character newCharacter = null;
            float timer = 0f;

            // Wait until Character is initialized (with timeout)
            while (newCharacter == null)
            {
                newCharacter = PlayerHandler.GetPlayerCharacter(newPlayer);
                if (newCharacter != null) break;

                yield return null;
                timer += Time.deltaTime;
                if (timer > CharacterWaitTimeoutSeconds)
                {
                    _logger?.LogWarning($"[LateJoin] Timeout waiting for Character for player {newPlayer.NickName} (actor {newPlayer.ActorNumber}). Aborting late-join handling.");
                    yield break;
                }
            }

            // Wait until CharacterData exists (timeout)
            timer = 0f;
            while (newCharacter.data == null)
            {
                yield return null;
                timer += Time.deltaTime;
                if (timer > CharacterWaitTimeoutSeconds)
                {
                    _logger?.LogWarning($"[LateJoin] Timeout waiting for CharacterData for {newCharacter.characterName}. Aborting.");
                    yield break;
                }
            }

            // Small delay to avoid clashing with the game's built-in RPC_SyncOnJoin
            yield return new WaitForSeconds(InitialJoinWaitSeconds);

            bool wasDead = false;
            string savedStage = null;

            // Read previously saved state from room properties (if available)
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
            {
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue($"dead_{newPlayer.ActorNumber}", out object deadState))
                {
                    wasDead = deadState is bool b && b;
                }
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue($"stage_{newPlayer.ActorNumber}", out object stageState))
                {
                    savedStage = stageState as string;
                }
            }
            else
            {
                _logger?.LogWarning("[LateJoin] Not in room while handling late join; skipping saved state read.");
            }

            string currentStage = SceneManager.GetActiveScene().name;

            _logger?.LogInfo($"[LateJoin] {newCharacter.characterName} joined. SavedStage={savedStage}, CurrentStage={currentStage}, WasDead={wasDead}");

            // Ensure they spawn after RPC sync has settled
            yield return new WaitForSeconds(AfterSyncWaitSeconds);

            ImprovedSpawnTarget spawnTarget = PopulateSpawnData(newCharacter);

            if (spawnTarget.LowestCharacter == null)
            {
                _logger.LogWarning($"[LateJoin] No valid spawn target found. Keeping {newCharacter.characterName} at spawn.");
                yield break;
            }

            Vector3 targetPos = spawnTarget.LowestCharacter.data != null && spawnTarget.LowestCharacter.data.isGrounded
                ? spawnTarget.LowestCharacter.data.groundPos
                : spawnTarget.LowestCharacter.transform.position;

            _logger.LogInfo($"[LateJoin] {newCharacter.characterName} → warping near {spawnTarget.LowestCharacter.characterName} at Y={targetPos.y:F2}");

            // Try updating spawnPoint before teleport (engine may override position)
            if (newCharacter.data.spawnPoint != null)
            {
                try
                {
                    newCharacter.data.spawnPoint.position = targetPos;
                }
                catch (System.Exception ex)
                {
                    _logger?.LogWarning($"[LateJoin] Failed to update spawnPoint: {ex.Message}");
                }
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

            // Save death state for future joiners
            SaveDeathState(newPlayer, newCharacter.data.dead);
            _lastKnownDead[newPlayer.ActorNumber] = newCharacter.data.dead;

            _logger.LogInfo($"[LateJoin] Finished processing {newCharacter.characterName}.");
        }

        private static ImprovedSpawnTarget PopulateSpawnData(Character newCharacter)
        {
            var result = new ImprovedSpawnTarget();

            foreach (Character c in Character.AllCharacters)
            {
                if (c == null || c == newCharacter || c.data == null || c.data.dead)
                    continue;

                float heightY = c.data.isGrounded ? c.data.groundPos.y : c.transform.position.y;
                result.RegisterCharacter(c, heightY);
            }

            return result;
        }

        private void SaveDeathState(Photon.Realtime.Player player, bool isDead)
        {
            if (player == null)
                return;

            // Only set properties when in a valid room
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            {
                _logger?.LogWarning($"[SaveDeathState] Not in room; can't save death state for actor {player.ActorNumber}.");
                return;
            }

            var props = new ExitGames.Client.Photon.Hashtable
            {
                { $"dead_{player.ActorNumber}", isDead },
                { $"stage_{player.ActorNumber}", SceneManager.GetActiveScene().name }
            };

            try
            {
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }
            catch (System.Exception ex)
            {
                _logger?.LogWarning($"[SaveDeathState] SetCustomProperties failed: {ex.Message}");
            }
        }
    }
}
