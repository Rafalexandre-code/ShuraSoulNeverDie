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
        /// <summary>A floor blast goes off. Magnitude = radius.</summary>
        Explosion,
        /// <summary>A puff of smoke (burrowing, blinking, summoning, evading). Magnitude = size.</summary>
        Smoke,
        /// <summary>An ally mended by a healer. Magnitude = body radius.</summary>
        Heal,
        /// <summary>The healer's aura pulses. Magnitude = aura radius.</summary>
        HealPulse,
        /// <summary>A blade passes through an ethereal enemy.</summary>
        Phase,
        /// <summary>An enemy enters its frenzy.</summary>
        Enrage,
        /// <summary>Lifesteal: health flows back to the enemy. Magnitude = amount.</summary>
        Drain,
        /// <summary>The duelist enters Mushin.</summary>
        Mushin,
        /// <summary>A new guard. Magnitude = Kamae index.</summary>
        Kamae,
        /// <summary>The breath window after the last blow of a combo.</summary>
        BreathGlint,
        /// <summary>A dodge inside the breath window.</summary>
        Breath,
        /// <summary>Seals detonated. Magnitude = seal count.</summary>
        SealBurst,
        /// <summary>A war cry. Magnitude = radius.</summary>
        Kiai,
        /// <summary>A curse takes hold.</summary>
        Curse,
        /// <summary>Shura crosses a level. Magnitude = new level.</summary>
        ShuraRise,
        /// <summary>The fall into the Shuradō.</summary>
        AsuraFall,
        /// <summary>A named weapon Link.</summary>
        Elo,
        /// <summary>The bronze bell rings. Magnitude = radius.</summary>
        Bell,
        /// <summary>The standoff begins.</summary>
        Standoff,
        /// <summary>A standoff feint (the enemy twitches).</summary>
        StandoffFeint,
        /// <summary>The real advance: draw now.</summary>
        StandoffAdvance,
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
        Ethereal,
        Healer,
        Burrow,
        Stance,
        Blast,
        Charge,
        Enrage,
        Lifesteal,
        Reassemble,
        DeathBlast,
        Clones,
        Summoner,
        Split,
        Mushin,
        Kamae,
        Stamina,
        Seals,
        Kiai,
        Gourd,
        Tools,
        Wounds,
        Curse,
        Shura,
        Asura,
        Elo,
        FinisherCut,
        MasterAnswers,
        Feint,
        Infight,
        Standoff,
        InnerAsura,
        GroupBlow,
        EliteAffixes,
        Route,
        Encounter,
        Biome,
        Weather,
        Corruption,
        Arena,
        Challenge,
        Dispute,
        Ally,
        Kalpa,
        SpiritBlow,
        Reactions,
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
