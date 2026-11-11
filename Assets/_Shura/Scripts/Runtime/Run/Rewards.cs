using System;
using UnityEngine;

namespace Shura
{
    public enum RewardKind
    {
        Technique,
        Relic,
        /// <summary>Small, repeatable, cost-free growth: keeps an endless run rewarding.</summary>
        Blessing,
        /// <summary>Dropped equipment, worn in one of four slots for the rest of the run.</summary>
        Item,
    }

    /// <summary>
    /// Aggregated effect of every owned reward. Rewards only touch these fields or the closed
    /// set of hooks on RewardDef, which keeps combinations predictable.
    /// </summary>
    public sealed class PlayerModifiers
    {
        public float damageMult = 1f;
        public float postureMult = 1f;
        public float normalAttackPostureMult = 1f;
        public float perfectParryPostureMult = 1f;
        public float blockWearMult = 1f;
        public float recoveryMult = 1f;
        public float dodgeRechargeMult = 1f;
        public float flowGainMult = 1f;
        public float damageTakenMult = 1f;
        public float damageTakenAtMaxFlowMult = 1f;
        public int perfectParryFrameBonus;
        public int dodgeChargeBonus;
        public int extraFlowLossOnHurt;
        public float maxHealthBonus;
        public float maxPostureBonus;
        public float finisherHeal;
        public float roomClearHealBonus;
        public float startFlowPoints;
        public int extraRewardChoices;
        public float moveSpeedMult = 1f;
        public float dodgeDistanceMult = 1f;
        /// <summary>Chance (0..1) for a hit to deal critical damage.</summary>
        public float critChance;
        /// <summary>Fraction of damage dealt returned as health.</summary>
        public float lifesteal;
        /// <summary>Element that colours the slash trail (first equipped elemental item).</summary>
        public Element trailElement;

        // ── Relics of the road (design doc §4.8) ───────────────────────────────────────
        public float shuraGainMult = 1f;
        public float innerAsuraHealthMult = 1f;
        /// <summary>Dodge charges are never spent.</summary>
        public bool infiniteDodge;
        /// <summary>Holding parry does not block (the perfect window still parries).</summary>
        public bool noBlock;
        /// <summary>Perfect actions in a row that open Mushin (0 = the build's own).</summary>
        public int mushinStreak;
        public bool hideHud;
        /// <summary>Interact near an enemy grabs it.</summary>
        public bool grab;
        /// <summary>How early the timing rings start closing (1 = normal).</summary>
        public float ringWarningMult = 1f;
        public float ecosMult = 1f;
        /// <summary>See each enemy's next blow.</summary>
        public bool dragonEye;
        /// <summary>How fast the enemies learn this duelist.</summary>
        public float adaptMult = 1f;

        // ── Item identity (design doc §4.6) ──────────────────────────────────────────
        /// <summary>Rosa de Ferro: a block is half a parry.</summary>
        public bool ironRose;
        /// <summary>Pena do Tengu: the dodge is a short teleport.</summary>
        public bool tenguDodge;
        /// <summary>Sino Rachado: Mushin survives the first wound.</summary>
        public bool mushinSurvivesHit;
        /// <summary>A corrupted item's flaw: the timing rings are not drawn.</summary>
        public bool hiddenRings;
    }

    /// <summary>
    /// A technique or relic. Hooks are the closed set of moments a reward may react to.
    /// Instances hold per-run state, so a fresh instance is created for every run.
    /// </summary>
    public abstract class RewardDef
    {
        public abstract string Id { get; }
        public abstract RewardKind Kind { get; }
        public abstract string Name { get; }
        public abstract string Gain { get; }
        public abstract string Cost { get; }
        public virtual string SourceEnemy => null;
        /// <summary>Stackable rewards can be taken many times in one run.</summary>
        public virtual bool Stackable => false;
        /// <summary>Health restored when picked.</summary>
        public virtual float HealOnPick => 0f;

        public virtual void Apply(PlayerModifiers m) { }
        public virtual void OnPerfectParry(World world, Enemy attacker) { }
        public virtual void OnPerfectDodge(World world, Enemy attacker) { }
        public virtual void ModifyAttackPosture(World world, Enemy target, ref float posture) { }
        public virtual void ModifyAttackDamage(World world, Enemy target, ref float damage) { }
        public virtual void ModifyParryPosture(World world, Enemy attacker, ref float posture) { }
        public virtual void OnBlock(World world, Enemy attacker) { }
        public virtual void OnFinisher(World world, Enemy target) { }
        /// <summary>After a player strike actually wounds an enemy (not when it is deflected).</summary>
        public virtual void OnDealtDamage(World world, Enemy target, float damage) { }
        /// <summary>When a combo strike's active frames begin.</summary>
        public virtual void OnSwing(World world, int comboIndex, Vector2 direction) { }
        /// <summary>When any dodge starts, from where it starts.</summary>
        public virtual void OnDodge(World world, Vector2 from) { }
        /// <summary>When an enemy dies in the party's room (any cause; minions included).</summary>
        public virtual void OnKill(World world, Enemy enemy) { }
        /// <summary>A blow is about to drop the player to 0: return true to survive it at 1 health.</summary>
        public virtual bool OnLethalHit(World world) => false;
        /// <summary>The duelist took a wound (after it landed).</summary>
        public virtual void OnHurt(World world) { }
        /// <summary>Just taken (after the build's numbers are recomputed).</summary>
        public virtual void OnAcquired(PlayerBuild build) { }
    }

    /// <summary>A blessing is a small permanent-for-this-run boost that can stack.</summary>
    public sealed class Blessing : RewardDef
    {
        readonly string id, name, gain;
        readonly float heal;
        readonly Action<PlayerModifiers> apply;

        public Blessing(string id, string name, string gain, float heal, Action<PlayerModifiers> apply)
        {
            this.id = id;
            this.name = name;
            this.gain = gain;
            this.heal = heal;
            this.apply = apply;
        }

        public override string Id => id;
        public override RewardKind Kind => RewardKind.Blessing;
        public override string Name => name;
        public override string Gain => gain;
        public override string Cost => "Nenhum.";
        public override bool Stackable => true;
        public override float HealOnPick => heal;
        public override void Apply(PlayerModifiers m) => apply(m);
    }

    /// <summary>A relic is a pure stat trade-off.</summary>
    public sealed class Relic : RewardDef
    {
        readonly string id, name, gain, cost;
        readonly Action<PlayerModifiers> apply;

        public Relic(string id, string name, string gain, string cost, Action<PlayerModifiers> apply)
        {
            this.id = id;
            this.name = name;
            this.gain = gain;
            this.cost = cost;
            this.apply = apply;
        }

        public override string Id => id;
        public override RewardKind Kind => RewardKind.Relic;
        public override string Name => name;
        public override string Gain => gain;
        public override string Cost => cost;
        public override void Apply(PlayerModifiers m) => apply(m);
    }
}
