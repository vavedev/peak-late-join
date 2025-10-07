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
        /// Uses ground position if available, otherwise transform position.
        /// Includes physics freeze, collision ignore, and stable network sync.
        /// </summary>
        public static IEnumerator SafeWarp(Character newCharacter, Character targetCharacter, ManualLogSource logger)
        {
            if (newCharacter == null || targetCharacter == null)
                yield break;

            Rigidbody rb = newCharacter.GetComponent<Rigidbody>();
            Collider newCol = newCharacter.GetComponent<Collider>();
            Collider targetCol = targetCharacter.GetComponent<Collider>();

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (newCol != null && targetCol != null)
                Physics.IgnoreCollision(newCol, targetCol, true);

            Vector3 targetPos = targetCharacter.data != null && targetCharacter.data.isGrounded
                ? targetCharacter.data.groundPos
                : targetCharacter.transform.position;

            Vector3[] offsets =
            {
                -targetCharacter.transform.forward * 1.5f,
                targetCharacter.transform.right * 1.5f,
                -targetCharacter.transform.right * 1.5f,
                targetCharacter.transform.forward * 1.5f
            };

            List<Vector3> safeSpots = new();

            foreach (var offset in offsets)
            {
                Vector3 candidate = targetPos + offset + Vector3.up * 0.1f;

                if (Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float diff = Mathf.Abs(hit.point.y - targetPos.y);
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
                safePos = targetPos + Vector3.up * 1.5f;
            }

            logger.LogInfo($"[SafeWarp] Warping {newCharacter.characterName} near {targetCharacter.characterName} → Y={safePos.y:F2}");

            if (newCharacter.photonView != null)
            {
                newCharacter.photonView.RPC(
                    "WarpPlayerRPC",
                    RpcTarget.All,
                    new object[] { safePos, false }
                );
            }

            yield return new WaitForSeconds(0.5f);

            if (rb != null)
            {
                rb.position = safePos;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
            }

            // --- STEP 8: Wait before re-enabling collision ---
            yield return new WaitForSeconds(0.5f);

            if (newCol != null && targetCol != null)
                Physics.IgnoreCollision(newCol, targetCol, false);
        }
    }
}
