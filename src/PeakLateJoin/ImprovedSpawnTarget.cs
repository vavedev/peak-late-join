using UnityEngine;

namespace PeakLateJoin
{
    public struct ImprovedSpawnTarget
    {
        public Character LowestCharacter;
        private float _lowestY;

        public void RegisterCharacter(Character character, float y)
        {
            if (LowestCharacter == null || y < _lowestY)
            {
                LowestCharacter = character;
                _lowestY = y;
            }
        }
    }
}
