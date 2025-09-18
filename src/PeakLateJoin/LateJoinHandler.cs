using System.Collections;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakLateJoin
{
    public class LateJoinHandler : MonoBehaviourPunCallbacks
    {
        private ManualLogSource _logger;

        public void InitLogger(ManualLogSource logger)
        {
            _logger = logger;
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != "Airport")
            {
                StartCoroutine(HandleLateJoin(newPlayer));
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

            yield return new WaitForSeconds(0.5f);

            ImprovedSpawnTarget spawnTarget = PopulateSpawnData(newCharacter);
            if (spawnTarget.LowestCharacter == null)
            {
                _logger.LogError("No valid spawn target found!");
                yield break;
            }

            yield return TeleportUtils.SafeWarp(newCharacter, spawnTarget.LowestCharacter, _logger);
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
    }
}
