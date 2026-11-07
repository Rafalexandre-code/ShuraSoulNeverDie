using UnityEngine;

namespace Shura
{
    /// <summary>Gameplay moments the presentation layer reacts to.</summary>
    public enum FxType
    {
        PlayerSwing,
        EnemySwing,
        HitEnemy,
        EnemyBlocked,
        PlayerBlock,
        PerfectParry,
        PerfectDodge,
        CounterThrust,
        PostureBreak,
        Finisher,
        PlayerHurt,
        GuardBreak,
        PlayerDeath,
        EnemyDeath,
        DangerCue,
        Dodge,
        Afterimage,
        FlowUp,
        FlowDown,
        TechniqueAbsorbed,
        Mastered,
        Spawn,
        RoomClear,
        Burst,
        MirrorThrust,
        /// <summary>The timing ring closed: "answer now". Magnitude 1 = dangerous (dodge), 0 = parry.</summary>
        TimingCue,
        Footstep,
        /// <summary>The Sombra vanishing or reappearing.</summary>
        ShadowStep,
        /// <summary>Every 5 perfect actions in a row. Magnitude = streak length.</summary>
        StreakMilestone,
        /// <summary>Troca Perfeita: swap-parry. Colour = incoming weapon.</summary>
        SwapParry,
        /// <summary>The special attack after a Troca Perfeita starts its active frames.</summary>
        SwapStrike,
        /// <summary>An ordinary weapon change.</summary>
        WeaponSwap,
        /// <summary>Thrust/jab streak. Magnitude = length.</summary>
        ThrustTrail,
        SlamImpact,
        /// <summary>A finisher begins. Magnitude = FinisherStyle.</summary>
        FinisherStart,
        /// <summary>A blow inside a finisher. Magnitude = intensity.</summary>
        FinisherHit,
        Crit,
        /// <summary>Burn/bleed damage tick. Magnitude = damage.</summary>
        DotTick,
        /// <summary>Lightning arc. Magnitude = length.</summary>
        Lightning,
        /// <summary>An item hits the floor. Magnitude = rarity tier.</summary>
        ItemDrop,
    }

    public enum HintId
    {
        Controls,
        Parry,
        DangerThrust,
        DangerSweep,
        DangerGrab,
        Projectile,
        Shadow,
        Finisher,
        Mastery,
        SwapParry,
    }

    /// <summary>
    /// Combat talks to presentation only through this interface, so the simulation stays
    /// testable and never knows about cameras, sounds or particles.
    /// </summary>
    public interface IFeedback
    {
        void Emit(FxType type, Vector2 position, Vector2 direction, float magnitude = 1f, Color? color = null);
        void Popup(string text, Vector2 worldPosition, Color color);
        void Toast(string text, Color color);
        void Hint(HintId hint);
        /// <summary>Blood spray (sparks when blood is disabled). Amount ≈ droplets.</summary>
        void Blood(Vector2 position, Vector2 direction, float amount, Color color);
        /// <summary>The body's fate at the end of a finisher.</summary>
        void Gore(FinisherStyle style, Vector2 position, Vector2 direction, Sprite body, Color bodyColor, Color blood, float radius);
        void ItemCollected(Item item, bool equipped, Vector2 position);
        /// <summary>The player's foot touches the ground.</summary>
        void Footstep(Vector2 position);
    }

    /// <summary>Used by tests and headless runs where nothing needs to be shown.</summary>
    public sealed class NullFeedback : IFeedback
    {
        public void Emit(FxType type, Vector2 position, Vector2 direction, float magnitude = 1f, Color? color = null) { }
        public void Popup(string text, Vector2 worldPosition, Color color) { }
        public void Toast(string text, Color color) { }
        public void Hint(HintId hint) { }
        public void Blood(Vector2 position, Vector2 direction, float amount, Color color) { }
        public void Gore(FinisherStyle style, Vector2 position, Vector2 direction, Sprite body, Color bodyColor, Color blood, float radius) { }
        public void ItemCollected(Item item, bool equipped, Vector2 position) { }
        public void Footstep(Vector2 position) { }
    }
}
