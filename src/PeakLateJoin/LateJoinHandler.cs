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

            // Wait until the Character object exists
            while (newCharacter == null)
            {
                newCharacter = PlayerHandler.GetPlayerCharacter(newPlayer);
                yield return null;
            }

            // Wait until the character is fully initialized and alive
            while (newCharacter.data == null || newCharacter.data.dead)
            {
                yield return null;
            }

            // Extra buffer to allow Photon to finish sync
            yield return new WaitForSeconds(0.5f);

            // Find a safe spawn target
            ImprovedSpawnTarget spawnTarget = PopulateSpawnData(newCharacter);
            if (spawnTarget.LowestCharacter == null)
            {
                _logger.LogError("No valid spawn target found.");
                yield break;
            }

            _logger.LogInfo($"Peak late join: {newCharacter.characterName} will be warped near {spawnTarget.LowestCharacter.characterName}");

            // Warp safely
            yield return TeleportUtils.SafeWarp(newCharacter, spawnTarget.LowestCharacter, _logger);

            // Ensure player is alive after warp
            if (newCharacter.data.dead)
            {
                newCharacter.data.dead = false;
                _logger.LogInfo($"Revived {newCharacter.characterName} from ghost state after warp.");
            }
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
