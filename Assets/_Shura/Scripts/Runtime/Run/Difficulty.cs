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
        /// <summary>
        /// Past the first Kalpa the numbers climb more slowly (the new rules carry the
        /// difficulty instead): each room beyond 25 counts as 0.6 of a room.
        /// </summary>
        public static float ScalingDepth(int depth) => depth <= RouteMap.KalpaLength ? depth : RouteMap.KalpaLength + (depth - RouteMap.KalpaLength) * 0.6f;

        public static float EnemyHealthMult(int depth) => 1f + 0.08f * ScalingDepth(depth);
        public static float EnemyPostureMult(int depth) => 1f + 0.06f * ScalingDepth(depth);
        public static float EnemyDamageMult(int depth) => 1f + 0.05f * ScalingDepth(depth);
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
        // Adaptive AI 2.0 (design doc §5.6).
        float backAttacks, tools, swaps, drinks;
        float combatSeconds;

        /// <summary>"O Shura Lembra": what the last runs taught them, a floor under the reading.</summary>
        public AdaptiveTraits Baseline;

        public float AttacksPerSecond => attacks / Window;
        public float DodgesPerSecond => dodges / Window;
        public float PerfectsPerSecond => perfects / Window;
        public float EarlyParryRatio => parryPresses >= 2f ? earlyParries / parryPresses : 0f;
        public bool WarmedUp => combatSeconds >= WarmupSeconds;
        public float BackAttacksPerSecond => backAttacks / Window;
        public float ToolsPerSecond => tools / Window;
        public float SwapsPerSecond => swaps / Window;
        public float DrinksPerSecond => drinks / Window;

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
            backAttacks *= k;
            tools *= k;
            swaps *= k;
            drinks *= k;
        }

        /// <summary>Smoke bomb: whatever they had read about you is gone.</summary>
        public void Reset()
        {
            attacks = dodges = perfects = parryPresses = earlyParries = 0f;
            backAttacks = tools = swaps = drinks = 0f;
            combatSeconds = 0f;
            Baseline = default;
        }

        public void OnAttack() => attacks += 1f;
        public void OnDodge() => dodges += 1f;
        public void OnPerfect() => perfects += 1f;

        public void OnParryPress(bool early)
        {
            parryPresses += 1f;
            if (early) earlyParries += 1f;
        }

        public void OnBackAttack() => backAttacks += 1f;
        public void OnTool() => tools += 1f;
        public void OnSwapParry() => swaps += 1f;
        public void OnDrink()
        {
            drinks += 1f;
            TotalDrinks++;
        }

        /// <summary>Goles bebidos na run inteira (telemetria, WP-01).</summary>
        public int TotalDrinks { get; private set; }

        /// <summary>
        /// Counter-reading (§5.6): the player did the opposite of what was read. That reading
        /// is wiped (and its memory from past runs with it).
        /// </summary>
        public void Forget(ReadingId id)
        {
            switch (id)
            {
                case ReadingId.Delay: earlyParries = 0f; break;
                case ReadingId.Guard: attacks *= 0.3f; break;
                case ReadingId.Pressure: attacks = Mathf.Max(attacks, 0.5f * Window); break;
                case ReadingId.Track: dodges = 0f; break;
                case ReadingId.Danger: perfects = 0f; break;
                case ReadingId.Flank: backAttacks = 0f; break;
                case ReadingId.Tool: tools = 0f; break;
                case ReadingId.Swap: swaps = 0f; break;
                case ReadingId.Heal: drinks = 0f; break;
            }
            Baseline.Set(id, 0f);
        }

        public AdaptiveTraits Traits(float strength)
        {
            var t = new AdaptiveTraits { strength = strength };
            if (WarmedUp)
            {
                t.delay = Mathf.Clamp01((EarlyParryRatio - 0.25f) / 0.5f);
                t.guard = Mathf.Clamp01((AttacksPerSecond - 1.1f) / 1.4f);
                t.pressure = Mathf.Clamp01((0.45f - AttacksPerSecond) / 0.45f);
                t.track = Mathf.Clamp01((DodgesPerSecond - 0.35f) / 0.6f);
                t.danger = Mathf.Clamp01((PerfectsPerSecond - 0.25f) / 0.6f);
                t.flank = Mathf.Clamp01((BackAttacksPerSecond - 0.12f) / 0.3f);
                t.tool = Mathf.Clamp01((ToolsPerSecond - 0.05f) / 0.15f);
                t.swap = Mathf.Clamp01((SwapsPerSecond - 0.07f) / 0.18f);
                t.heal = Mathf.Clamp01((DrinksPerSecond - 0.02f) / 0.06f);
            }
            // What the Shura remembers from earlier lives is a floor, not a ceiling.
            foreach (ReadingId id in AdaptiveTraits.All) t.Set(id, Mathf.Max(t.Get(id), Baseline.Get(id) * 0.8f));
            return t;
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
        public float flank;
        public float tool;
        public float swap;
        public float heal;

        public static readonly ReadingId[] All =
        {
            ReadingId.Delay, ReadingId.Guard, ReadingId.Pressure, ReadingId.Track, ReadingId.Danger,
            ReadingId.Flank, ReadingId.Tool, ReadingId.Swap, ReadingId.Heal,
        };

        /// <summary>The raw tendency (0..1) of one reading.</summary>
        public float Get(ReadingId id)
        {
            switch (id)
            {
                case ReadingId.Delay: return delay;
                case ReadingId.Guard: return guard;
                case ReadingId.Pressure: return pressure;
                case ReadingId.Track: return track;
                case ReadingId.Danger: return danger;
                case ReadingId.Flank: return flank;
                case ReadingId.Tool: return tool;
                case ReadingId.Swap: return swap;
                default: return heal;
            }
        }

        public void Set(ReadingId id, float v)
        {
            switch (id)
            {
                case ReadingId.Delay: delay = v; break;
                case ReadingId.Guard: guard = v; break;
                case ReadingId.Pressure: pressure = v; break;
                case ReadingId.Track: track = v; break;
                case ReadingId.Danger: danger = v; break;
                case ReadingId.Flank: flank = v; break;
                case ReadingId.Tool: tool = v; break;
                case ReadingId.Swap: swap = v; break;
                default: heal = v; break;
            }
        }

        /// <summary>How much of it they use (scaled by depth).</summary>
        public float Value(ReadingId id) => Get(id) * strength;

        /// <summary>The visible meter: 0 to 3 marks (§5.6).</summary>
        public int Level(ReadingId id)
        {
            float v = Value(id);
            return v < ActiveThreshold ? 0 : v < 0.55f ? 1 : v < 0.8f ? 2 : 3;
        }

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
        /// <summary>You strike from behind → they turn to face you faster.</summary>
        public float Flank => flank * strength;
        /// <summary>You throw a lot → they sidestep your projectiles.</summary>
        public float Tool => tool * strength;
        /// <summary>You live on the Perfect Swap → they break the rhythm (more held strikes).</summary>
        public float Swap => swap * strength;
        /// <summary>You drink a lot → they press hard once your gourd is empty.</summary>
        public float Heal => heal * strength;

        public List<ReadingId> Active()
        {
            var list = new List<ReadingId>();
            if (Delay >= ActiveThreshold) list.Add(ReadingId.Delay);
            if (Guard >= ActiveThreshold) list.Add(ReadingId.Guard);
            if (Pressure >= ActiveThreshold) list.Add(ReadingId.Pressure);
            if (Track >= ActiveThreshold) list.Add(ReadingId.Track);
            if (Danger >= ActiveThreshold) list.Add(ReadingId.Danger);
            if (Flank >= ActiveThreshold) list.Add(ReadingId.Flank);
            if (Tool >= ActiveThreshold) list.Add(ReadingId.Tool);
            if (Swap >= ActiveThreshold) list.Add(ReadingId.Swap);
            if (Heal >= ActiveThreshold) list.Add(ReadingId.Heal);
            return list;
        }
    }

    /// <summary>"O Shura Lembra": one run's final reading, kept for the next runs (§5.6).</summary>
    [System.Serializable]
    public sealed class ReadingMemory
    {
        public const int Keep = 5;
        public float[] values;

        public static ReadingMemory Of(AdaptiveTraits t)
        {
            var m = new ReadingMemory { values = new float[AdaptiveTraits.All.Length] };
            for (int i = 0; i < AdaptiveTraits.All.Length; i++) m.values[i] = Mathf.Clamp01(t.Get(AdaptiveTraits.All[i]));
            return m;
        }

        /// <summary>The average of the kept runs, as raw tendencies.</summary>
        public static AdaptiveTraits Average(List<ReadingMemory> list)
        {
            var t = new AdaptiveTraits();
            if (list == null || list.Count == 0) return t;
            for (int i = 0; i < AdaptiveTraits.All.Length; i++)
            {
                float sum = 0f;
                foreach (ReadingMemory m in list) sum += m.values[i];
                t.Set(AdaptiveTraits.All[i], sum / list.Count);
            }
            return t;
        }
    }

    public enum ReadingId
    {
        Delay,
        Guard,
        Pressure,
        Track,
        Danger,
        Flank,
        Tool,
        Swap,
        Heal,
    }
}
