using UnityEngine;

namespace PeakLateJoin
{
    /// <summary>
    /// Holds the lowest character found and its Y coordinate.
    /// </summary>
    public class ImprovedSpawnTarget
    {
        public Character LowestCharacter { get; private set; }
        private float _lowestY;

        public ImprovedSpawnTarget()
        {
            LowestCharacter = null;
            _lowestY = float.PositiveInfinity;
        }

        public void RegisterCharacter(Character character, float y)
        {
            if (character == null) return;

            // Choose first valid character or any with a lower Y value
            if (LowestCharacter == null || y < _lowestY)
            {
                LowestCharacter = character;
                _lowestY = y;
            }
        }
    }
}
