using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace PeakLateJoin
{
    public static class TeleportUtils
    {
        /// <summary>
        /// Safely teleports a character near the target character.
        /// - Prefers behind/around target if ground is safe (randomly picks one if multiple are safe)
        /// - Else falls back above target
        /// Prevents collisions and ragdoll bounce.
        /// </summary>
        public static IEnumerator SafeWarp(Character newCharacter, Character targetCharacter, ManualLogSource logger)
        {
            if (newCharacter == null || targetCharacter == null)
                yield break;

            Rigidbody rb = newCharacter.GetComponent<Rigidbody>();
            Collider newCol = newCharacter.GetComponent<Collider>();
            Collider targetCol = targetCharacter.GetComponent<Collider>();

            // Freeze physics before warp
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Temporarily ignore collision between the two characters
            if (newCol != null && targetCol != null)
                Physics.IgnoreCollision(newCol, targetCol, true);

            // Candidate spawn offsets (relative to target)
            Vector3[] offsets =
            {
                -targetCharacter.transform.forward * 1.5f, // behind
                targetCharacter.transform.right * 1.5f,   // right
                -targetCharacter.transform.right * 1.5f,  // left
                targetCharacter.transform.forward * 1.5f  // front
            };

            List<Vector3> safeSpots = new List<Vector3>();

            // Check each candidate with raycast
            foreach (var offset in offsets)
            {
                Vector3 candidate = targetCharacter.transform.position + offset + Vector3.up * 0.1f;

                if (Physics.Raycast(candidate + Vector3.up * 2f,
                                    Vector3.down, out RaycastHit hit, 5f,
                                    ~0, QueryTriggerInteraction.Ignore))
                {
                    float groundHeightDiff = Mathf.Abs(hit.point.y - targetCharacter.transform.position.y);

                    if (groundHeightDiff < 1.0f) // within 1m height difference
                    {
                        safeSpots.Add(hit.point + Vector3.up * 0.05f);
                    }
                }
            }

            // Pick a random safe spot if available, else fallback above target
            Vector3 safePos;
            if (safeSpots.Count > 0)
            {
                int index = Random.Range(0, safeSpots.Count);
                safePos = safeSpots[index];
            }
            else
            {
                safePos = targetCharacter.Head + Vector3.up * 2f;
            }

            logger.LogInfo($"Safely warping {newCharacter.characterName} {(safeSpots.Count > 0 ? "near" : "above")} {targetCharacter.characterName} at {safePos}");

            // Sync warp across network
            newCharacter.photonView.RPC(
                "WarpPlayerRPC",
                RpcTarget.All,
                new object[] { safePos, false }
            );

            // Wait a bit for network sync
            yield return new WaitForSeconds(0.3f);

            // Restore physics safely
            if (rb != null)
            {
                rb.position = safePos;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
            }

            // Re-enable collisions after physics is stable
            yield return new WaitForSeconds(0.3f);
            if (newCol != null && targetCol != null)
                Physics.IgnoreCollision(newCol, targetCol, false);
        }
    }
}
