using UnityEngine;

namespace Shura
{
    /// <summary>Animated effect strips from the Ninja Adventure pack, cut once and cached.</summary>
    public static class FxStrips
    {
        static Sprite[] explosion, smoke, aura;

        static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        /// <summary>9 frames of 40 × 40 px.</summary>
        public static Sprite[] Explosion => explosion ?? (explosion = PixelArt.Strip("fx_explosion", 40, 40, Centre));
        /// <summary>6 frames of 32 × 32 px.</summary>
        public static Sprite[] Smoke => smoke ?? (smoke = PixelArt.Strip("fx_smoke", 32, 32, Centre));
        /// <summary>5 frames of 25 × 24 px.</summary>
        public static Sprite[] Aura => aura ?? (aura = PixelArt.Strip("fx_aura", 25, 24, Centre));
    }
}
