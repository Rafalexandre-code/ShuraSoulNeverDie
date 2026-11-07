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
    }

    /// <summary>Used by tests and headless runs where nothing needs to be shown.</summary>
    public sealed class NullFeedback : IFeedback
    {
        public void Emit(FxType type, Vector2 position, Vector2 direction, float magnitude = 1f, Color? color = null) { }
        public void Popup(string text, Vector2 worldPosition, Color color) { }
        public void Toast(string text, Color color) { }
        public void Hint(HintId hint) { }
    }
}
