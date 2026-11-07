using System;

namespace Shura
{
    public enum RewardKind
    {
        Technique,
        Relic,
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

        public virtual void Apply(PlayerModifiers m) { }
        public virtual void OnPerfectParry(World world, Enemy attacker) { }
        public virtual void OnPerfectDodge(World world, Enemy attacker) { }
        public virtual void ModifyAttackPosture(World world, Enemy target, ref float posture) { }
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
