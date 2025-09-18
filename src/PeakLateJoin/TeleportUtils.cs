using System.Collections;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace PeakLateJoin
{
    public static class TeleportUtils
    {
        /// <summary>
        /// Safely teleports a character to the target character's position,
        /// disabling physics during warp to prevent ragdolls and collision glitches.
        /// </summary>
        public static IEnumerator SafeWarp(Character newCharacter, Character targetCharacter, ManualLogSource logger)
        {
            if (newCharacter == null || targetCharacter == null)
                yield break;

            Rigidbody rb = newCharacter.GetComponent<Rigidbody>();

            // Freeze physics before warp
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Offset above the target to avoid collider overlap
            Vector3 safePos = targetCharacter.Head + Vector3.up * 2f;
            logger.LogInfo($"Safely warping {newCharacter.characterName} to {targetCharacter.characterName} at {safePos}");

            // Use Photon RPC for network sync
            newCharacter.photonView.RPC(
                "WarpPlayerRPC",
                RpcTarget.All,
                new object[] { safePos, false }
            );

            // Small delay before restoring physics
            yield return new WaitForSeconds(0.2f);

            if (rb != null)
                rb.isKinematic = false;
        }
    }
}
