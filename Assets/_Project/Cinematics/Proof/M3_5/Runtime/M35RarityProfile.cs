#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using UnityEngine;

namespace ChessButWeird.CinematicProof.M35
{
    [CreateAssetMenu(menuName="Chess But Weird/M35 Mock Rarity Profile")]
    public sealed class M35RarityProfile:ScriptableObject
    {
        public MockRarity rarity;
        public Color accent;
        public float duration=7,cameraMotion=1,heroScale=1,lightPeak=2.3f;
        public int motesPerSecond=20,burstCount=40;
        public bool orbitSymbols,coronationHalo;
        public AudioClip revealCue;
    }
}


#endif
