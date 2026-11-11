using UnityEngine;

namespace Shura
{
    /// <summary>Weapon mastery in the hand: what each level does while fighting (see <see cref="WeaponMastery"/>).</summary>
    public sealed partial class PlayerController
    {
        bool ougiUsed;
        bool ougiActive;

        /// <summary>Whose progress counts: an online guest's own, otherwise the run's (the host's).</summary>
        MetaProgress MasteryMeta => Build != null && Build.OwnMeta != null ? Build.OwnMeta : world.Run?.Meta;

        public int MasteryOf(WeaponDef w)
        {
            MetaProgress m = MasteryMeta;
            return m != null && w != null ? WeaponMastery.LevelFor(m.MasteryFinishers(w.id)) : 0;
        }

        float MasteryPostureMult => MasteryOf(Weapon) >= 1 ? 1.15f : 1f;

        float MasterySwapDamageMult => MasteryOf(Weapon) >= 3 ? 1.2f : 1f;

        /// <summary>The Ougi (or not) as a swap strike begins.</summary>
        void BeginSwapStrikeMastery()
        {
            ougiActive = false;
            if (ougiUsed || MasteryOf(Weapon) < WeaponMastery.MaxLevel) return;
            ougiUsed = true;
            ougiActive = true;
            Color c = WeaponMastery.BladeColor(Weapon);
            world.Fx.Popup(WeaponMastery.OugiName(Weapon).ToUpperInvariant(), Position + Vector2.up * 1.6f, c);
            world.Fx.Emit(FxType.Elo, Position, Facing, 3f, c);
            world.Fx.Emit(FxType.Burst, Position, Facing, 2.6f, c);
        }

        float OugiMult => ougiActive && CurrentState == State.SwapStrike ? 2.5f : 1f;

        /// <summary>A finisher with the blade in hand: a paper back at level 2, and the progress (kept by this duelist's own game).</summary>
        public void OnFinisherMastery()
        {
            if (MasteryOf(Weapon) >= 2 && Build != null && Build.SpiritPapers < 12) Build.SpiritPapers++;
            if (world.TutorialRoom) return;
            if (this != world.Host)
            {
                // An online guest: their own game keeps the record; this copy only keeps the effects current.
                Build?.OwnMeta?.AddMasteryFinisher(Weapon.id);
                return;
            }
            if (world.Run?.Meta == null) return;
            int level = world.Run.Meta.AddMasteryFinisher(Weapon.id);
            if (level > 0) world.RaiseMastery(Weapon, level);
        }

        void ResetMasteryForRoom()
        {
            ougiUsed = false;
            ougiActive = false;
        }
    }
}
