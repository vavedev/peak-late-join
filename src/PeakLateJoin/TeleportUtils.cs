using System.Collections;
using BepInEx.Logging;
using UnityEngine;
using Photon.Pun;

namespace PeakLateJoin
{
    /// <summary>
    /// Teleport helper utilities.
    /// </summary>
    public static class TeleportUtils
    {
        /// <summary>
        /// Safely teleports a character near the target character.
        /// Freezes physics during teleport, tries to pick a safe spot near the target,
        /// Sends an RPC warp to all clients (if the character has a photonView), then restores physics.
        /// </summary>
        public static IEnumerator SafeWarp(Character newCharacter, Character targetCharacter, ManualLogSource logger)
        {
            if (newCharacter == null)
            {
                logger?.LogWarning("[SafeWarp] newCharacter is null. Aborting warp.");
                yield break;
            }

            if (targetCharacter == null)
            {
                logger?.LogWarning("[SafeWarp] targetCharacter is null. Aborting warp.");
                yield break;
            }

            Rigidbody rb = newCharacter.GetComponent<Rigidbody>();
            Collider newCol = newCharacter.GetComponent<Collider>();
            Collider targetCol = targetCharacter.GetComponent<Collider>();

            // Freeze physics if we have a rigidbody
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Temporarily ignore collisions between the two characters (if colliders exist)
            if (newCol != null && targetCol != null)
            {
                Physics.IgnoreCollision(newCol, targetCol, true);
            }

            // Determine a base position (prefer ground position)
            Vector3 baseTargetPos = (targetCharacter.data != null && targetCharacter.data.isGrounded)
                ? targetCharacter.data.groundPos
                : targetCharacter.transform.position;

            // Candidate offsets around the target
            Vector3[] offsets =
            {
                -targetCharacter.transform.forward * 1.5f,
                targetCharacter.transform.right * 1.5f,
                -targetCharacter.transform.right * 1.5f,
                targetCharacter.transform.forward * 1.5f
            };

            var safeSpots = new System.Collections.Generic.List<Vector3>();

            foreach (var offset in offsets)
            {
                Vector3 candidate = baseTargetPos + offset + Vector3.up * 0.1f;
                if (Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float diff = Mathf.Abs(hit.point.y - baseTargetPos.y);
                    if (diff < 1.5f)
                    {
                        safeSpots.Add(hit.point + Vector3.up * 0.05f);
                    }
                }
            }

            Vector3 safePos;
            if (safeSpots.Count > 0)
            {
                safePos = safeSpots[Random.Range(0, safeSpots.Count)];
            }
            else
            {
                safePos = baseTargetPos + Vector3.up * 1.5f;
            }

            logger?.LogInfo($"[SafeWarp] Warping {newCharacter.characterName} near {targetCharacter.characterName} → Y={safePos.y:F2}");

            try
            {
                if (newCharacter.photonView != null)
                {
                    newCharacter.photonView.RPC("WarpPlayerRPC", RpcTarget.All, new object[] { safePos, false });
                }
            }
            catch (System.Exception ex)
            {
                logger?.LogWarning($"[SafeWarp] RPC WarpPlayerRPC call failed: {ex.Message}");
            }

            // Give network/engine a moment to sync (small wait)
            yield return new WaitForSeconds(0.5f);

            // Force-apply position on local rigidbody if present (helps with desync)
            if (rb != null)
            {
                rb.position = safePos;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
            }
            else
            {
                // If no rigidbody, try to set transform
                newCharacter.transform.position = safePos;
            }

            // Give physics another moment to settle
            yield return new WaitForSeconds(0.5f);

            // Re-enable collision
            if (newCol != null && targetCol != null)
            {
                Physics.IgnoreCollision(newCol, targetCol, false);
            }
        }
    }
}