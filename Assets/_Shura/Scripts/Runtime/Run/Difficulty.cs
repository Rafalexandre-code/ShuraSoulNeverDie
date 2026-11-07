using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// How the endless run escalates. Enemies get tougher every room; the player grows
    /// with Ascension (automatic) and rewards, but a little slower, so depth stays a test.
    /// </summary>
    public static class DifficultyRules
    {
        public static float EnemyHealthMult(int depth) => 1f + 0.08f * depth;
        public static float EnemyPostureMult(int depth) => 1f + 0.06f * depth;
        public static float EnemyDamageMult(int depth) => 1f + 0.05f * depth;
        /// <summary>Shorter pauses between attacks (never below 55%).</summary>
        public static float EnemyIdleMult(int depth) => Mathf.Max(0.55f, 1f - 0.03f * depth);
        public static int AttackGapFrames(int baseGap, int depth) => Mathf.Max(8, baseGap - depth);

        /// <summary>How much of what the enemies learned they are allowed to use.</summary>
        public static float AdaptStrength(int depth) => Mathf.Clamp01(0.15f + 0.07f * depth);

        public const float EliteHealthMult = 2.2f;
        public const float ElitePostureMult = 1.6f;
        public const float EliteDamageMult = 1.15f;
        public const float EliteSizeMult = 1.2f;

        public const float AscensionHealth = 4f;
        public const float AscensionDamage = 0.03f;
        public const float AscensionPosture = 2f;
    }

    /// <summary>Per-enemy multipliers for its room's depth (and Guardian status).</summary>
    public struct EnemyScaling
    {
        public float health;
        public float posture;
        public float damage;
        public float idle;
        public bool elite;

        public static EnemyScaling For(int depth, bool elite)
        {
            return new EnemyScaling
            {
                health = DifficultyRules.EnemyHealthMult(depth) * (elite ? DifficultyRules.EliteHealthMult : 1f),
                posture = DifficultyRules.EnemyPostureMult(depth) * (elite ? DifficultyRules.ElitePostureMult : 1f),
                damage = DifficultyRules.EnemyDamageMult(depth) * (elite ? DifficultyRules.EliteDamageMult : 1f),
                idle = DifficultyRules.EnemyIdleMult(depth),
                elite = elite,
            };
        }

        public static EnemyScaling None => For(0, false);
    }

    /// <summary>
    /// What the enemies have learned about the player, as 0..1 tendencies. Built from
    /// decaying event counters (recent behaviour matters most, ~12 s memory).
    /// </summary>
    public sealed class PlayerProfile
    {
        const float MemorySeconds = 12f;
        const float WarmupSeconds = 6f;

        float attacks, dodges, perfects, parryPresses, earlyParries;
        float combatSeconds;

        public float AttacksPerSecond => attacks / Window;
        public float DodgesPerSecond => dodges / Window;
        public float PerfectsPerSecond => perfects / Window;
        public float EarlyParryRatio => parryPresses >= 2f ? earlyParries / parryPresses : 0f;
        public bool WarmedUp => combatSeconds >= WarmupSeconds;

        float Window => Mathf.Clamp(combatSeconds, 3f, MemorySeconds);

        /// <summary>Only time spent in combat counts, so menus and empty rooms don't skew it.</summary>
        public void Tick(float dt, bool inCombat)
        {
            if (!inCombat) return;
            combatSeconds = Mathf.Min(MemorySeconds * 4f, combatSeconds + dt);
            float k = Mathf.Exp(-dt / MemorySeconds);
            attacks *= k;
            dodges *= k;
            perfects *= k;
            parryPresses *= k;
            earlyParries *= k;
        }

        public void OnAttack() => attacks += 1f;
        public void OnDodge() => dodges += 1f;
        public void OnPerfect() => perfects += 1f;

        public void OnParryPress(bool early)
        {
            parryPresses += 1f;
            if (early) earlyParries += 1f;
        }

        public AdaptiveTraits Traits(float strength)
        {
            if (!WarmedUp) return new AdaptiveTraits { strength = strength };
            return new AdaptiveTraits
            {
                strength = strength,
                delay = Mathf.Clamp01((EarlyParryRatio - 0.25f) / 0.5f),
                guard = Mathf.Clamp01((AttacksPerSecond - 1.1f) / 1.4f),
                pressure = Mathf.Clamp01((0.45f - AttacksPerSecond) / 0.45f),
                track = Mathf.Clamp01((DodgesPerSecond - 0.35f) / 0.6f),
                danger = Mathf.Clamp01((PerfectsPerSecond - 0.25f) / 0.6f),
            };
        }
    }

    /// <summary>
    /// The enemies' current reading of the player. Each value is a raw tendency (0..1);
    /// the getters scale it by how much the depth lets them use it.
    /// Adaptation changes which moves come and when — never the telegraph, which stays truthful.
    /// </summary>
    public struct AdaptiveTraits
    {
        public const float ActiveThreshold = 0.3f;

        public float strength;
        public float delay;
        public float guard;
        public float pressure;
        public float track;
        public float danger;

        /// <summary>You parry early → some strikes are held longer (the ring waits too).</summary>
        public float Delay => delay * strength;
        /// <summary>You mash attacks → they guard and riposte more.</summary>
        public float Guard => guard * strength;
        /// <summary>You wait too much → they attack more often.</summary>
        public float Pressure => pressure * strength;
        /// <summary>You dodge a lot → their swings follow you a little longer before committing.</summary>
        public float Track => track * strength;
        /// <summary>Your parries are precise → more dangerous (unparryable) moves.</summary>
        public float Danger => danger * strength;

        public List<ReadingId> Active()
        {
            var list = new List<ReadingId>();
            if (Delay >= ActiveThreshold) list.Add(ReadingId.Delay);
            if (Guard >= ActiveThreshold) list.Add(ReadingId.Guard);
            if (Pressure >= ActiveThreshold) list.Add(ReadingId.Pressure);
            if (Track >= ActiveThreshold) list.Add(ReadingId.Track);
            if (Danger >= ActiveThreshold) list.Add(ReadingId.Danger);
            return list;
        }
    }

    public enum ReadingId
    {
        Delay,
        Guard,
        Pressure,
        Track,
        Danger,
    }
}
