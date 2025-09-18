using UnityEngine;

namespace PeakLateJoin
{
    public struct ImprovedSpawnTarget
    {
        public Character LowestCharacter;

        public float LowestClimbed =>
            LowestCharacter ? LowestCharacter.Center.y : float.PositiveInfinity;

        public void RegisterCharacter(Character character)
        {
            if (character.Center.y < LowestClimbed)
            {
                LowestCharacter = character;
            }
        }
    }
}
