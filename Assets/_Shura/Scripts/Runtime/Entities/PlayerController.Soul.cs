using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Soul transformation (design doc §4.5): a technique at level III keeps its enemy's soul.
    /// Once per room the duelist becomes that enemy for 8 s — its body, its strength and its
    /// way of fighting: the Carrasco grabs, the Onryō goes ethereal, the Olho Vigia throws
    /// lightning. Online, everyone sees the yokai.
    /// </summary>
    public sealed partial class PlayerController
    {
        public const int SoulFrames = 8 * SimClock.TickRate;

        public int SoulFramesLeft { get; private set; }
        public EnemyDef SoulForm { get; private set; }
        bool soulUsedThisRoom;
        bool soulHinted;

        public bool InSoul => SoulFramesLeft > 0 && SoulForm != null;

        /// <summary>The soul this duelist carries: the enemy behind the latest technique raised to III.</summary>
        public EnemyDef SoulAvailable
        {
            get
            {
                if (Build == null || Build.HonorBound) return null;
                for (int i = Build.Owned.Count - 1; i >= 0; i--)
                {
                    if (!(Build.Owned[i] is TechniqueDef t) || t.Level < TechniqueDef.MaxLevel) continue;
                    foreach (EnemyDef d in EnemyDefs.All)
                    {
                        if (d.techniqueId == t.Id) return d;
                    }
                }
                return null;
            }
        }

        public bool SoulReady => !soulUsedThisRoom && !InSoul && SoulAvailable != null;

        /// <summary>The art the others see: the yokai while transformed.</summary>
        public string NetArt => InSoul ? SoulForm.art : PartyArt[ArtIndex];

        float SoulDamageMult => InSoul ? 1.35f : 1f;

        void TrySoul()
        {
            EnemyDef soul = SoulAvailable;
            if (soul == null)
            {
                world.Fx.Popup(Strings.PopupNoSoul, Position + Vector2.up, pal.uiDim);
                return;
            }
            if (soulUsedThisRoom || InSoul)
            {
                world.Fx.Popup(Strings.PopupSoulSpent, Position + Vector2.up, pal.uiDim);
                return;
            }
            soulUsedThisRoom = true;
            SoulForm = soul;
            SoulFramesLeft = SoulFrames;
            SetCharacterArt(PixelArt.Character(soul.art));
            world.Fx.Emit(FxType.Spawn, Position, Vector2.up, Radius * 2f, soul.color);
            world.Fx.Emit(FxType.Burst, Position, Vector2.up, 2.4f, soul.color);
            world.Fx.Popup(string.Format(Strings.PopupSoul, soul.displayName.ToUpperInvariant()), Position + Vector2.up * 1.5f, soul.color);
            world.RequestSlowMo(0.25f);
        }

        void TickSoul()
        {
            if (!soulHinted && SoulReady && this == world.Host)
            {
                soulHinted = true;
                world.HintOnce(HintId.Soul);
            }
            if (!InSoul) return;
            // Ethereal souls cannot be touched while they last.
            if (SoulForm.ethereal) hurtInvuln = Mathf.Max(hurtInvuln, 2);
            if (--SoulFramesLeft > 0) return;
            EndSoul();
        }

        void EndSoul()
        {
            if (SoulForm != null)
            {
                world.Fx.Emit(FxType.Smoke, Position, Vector2.up, Radius * 1.6f, SoulForm.color);
                world.Fx.Popup(Strings.PopupSoulEnds, Position + Vector2.up * 1.2f, SoulForm.color);
            }
            SoulFramesLeft = 0;
            SoulForm = null;
            SetArt(ArtIndex);
        }

        /// <summary>A swing in soul form: the enemy's own way of striking rides along with it.</summary>
        void SoulSwing()
        {
            if (!InSoul) return;
            EnemyDef d = SoulForm;
            Enemy target = world.FindNearestEnemy(Position, null);
            Vector2 dir = Facing;
            switch (d.id)
            {
                case "olho":
                case "raijin":
                    if (target != null && Vector2.Distance(target.Position, Position) < 7f) world.ChainLightning(target, 2, 10f, 12f);
                    return;
                case "arqueiro":
                    world.FirePlayerArrow(Position, dir, 12f, 16f);
                    return;
                case "kunoichi":
                    world.FirePlayerKunai(Position, dir, 3, 30f, 6f, 8f);
                    return;
                case "bombardeiro":
                    world.ScheduleBurst(Position + dir * 2f, 18, 1.5f, 12f, 18f, World.BlastColor(BlastKind.Bomb), true);
                    return;
                case "lanterna":
                case "onmyoji":
                    if (target != null) world.FireWisp(Position, target.Position - Position, 10f, 12f);
                    return;
                default:
                    world.PlayerInstantStrike(Position, dir, HitShape.Arc(2.3f, 120f), 8f, 14f);
                    return;
            }
        }

        /// <summary>Some souls pull what they hit (the Carrasco's grab, the Kappa's current).</summary>
        float SoulPull => InSoul && (SoulForm.id == "carrasco" || SoulForm.id == "kappa") ? 1.6f : 0f;

        float SoulMoveMult => InSoul && SoulForm.flying ? 1.25f : 1f;

        void ResetSoulForRoom()
        {
            soulUsedThisRoom = false;
            if (InSoul) EndSoul();
        }
    }
}
