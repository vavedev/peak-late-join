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

        private readonly Dictionary<int, bool> _lastKnownDead = new Dictionary<int, bool>();

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

                    _logger?.LogInfo($"Updated death state for {c.characterName}: {(isDead ? "dead" : "alive")}");
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

            while (newCharacter == null)
            {
                newCharacter = PlayerHandler.GetPlayerCharacter(newPlayer);
                yield return null;
            }

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

            if (wasDead && savedStage == currentStage)
            {
                _logger.LogInfo($"Player {newCharacter.characterName} was dead in stage {currentStage}, keeping them dead.");
                newCharacter.data.dead = true;
                yield break;
            }

            while (newCharacter.data == null)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);

            ImprovedSpawnTarget spawnTarget = PopulateSpawnData(newCharacter);
            if (spawnTarget.LowestCharacter == null)
            {
                _logger.LogError("No valid spawn target found.");
                yield break;
            }

            _logger.LogInfo($"Peak late join: {newCharacter.characterName} will be warped near {spawnTarget.LowestCharacter.characterName}");

            yield return TeleportUtils.SafeWarp(newCharacter, spawnTarget.LowestCharacter, _logger);

            if (wasDead && savedStage != currentStage)
            {
                newCharacter.data.dead = false;
                _logger.LogInfo($"Revived {newCharacter.characterName} since stage changed ({savedStage} → {currentStage}).");
            }

            SaveDeathState(newPlayer, newCharacter.data.dead);
            _lastKnownDead[newPlayer.ActorNumber] = newCharacter.data.dead;
        }

        private static ImprovedSpawnTarget PopulateSpawnData(Character newCharacter)
        {
            ImprovedSpawnTarget result = default;
            foreach (Character allCharacter in Character.AllCharacters)
            {
                if (!allCharacter.data.dead && allCharacter != newCharacter)
                {
                    result.RegisterCharacter(allCharacter);
                }
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