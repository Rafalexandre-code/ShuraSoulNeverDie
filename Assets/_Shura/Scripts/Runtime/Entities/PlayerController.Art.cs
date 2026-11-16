using UnityEngine;

namespace Shura
{
    /// <summary>
    /// A Arte (技, WP-05): um encaixe, uma tecla (R / RT), duas cargas, cada finalização devolve
    /// uma. Substitui as ferramentas e os papéis espirituais (R/T), o Kiai (V), a Alma (B) e o
    /// detonar (G). Só vale com LegacyToolsAndKeys desligada (o padrão).
    /// </summary>
    public sealed partial class PlayerController
    {
        public ArtId Art => Build != null ? Build.Art : ArtId.Kiai;
        public int ArtCharges => Build != null ? Build.ArtCharges : 0;

        /// <summary>Usa a Arte. Só gasta carga quando faz algo (o Kiai sem golpe vermelho por perto não gasta).</summary>
        void UseArt()
        {
            if (Build == null) return;
            if (Build.ArtCharges <= 0)
            {
                world.Fx.Popup(Strings.PopupArtEmpty, Position + Vector2.up, pal.uiDim);
                return;
            }
            bool spent;
            Vector2 aim = Facing;
            switch (Build.Art)
            {
                case ArtId.Kiai: spent = ArtKiai(); break;
                case ArtId.Shuriken:
                    for (int i = -1; i <= 1; i++) world.ThrowShuriken(this, (Vector2)(Quaternion.Euler(0f, 0f, 14f * i) * aim));
                    spent = true;
                    break;
                case ArtId.Smoke:
                {
                    world.SmokeBomb(Position);
                    // Some na fumaça e reaparece 3 m atrás.
                    Vector2 back = Position - aim * 3f;
                    Position = world.Layout.Resolve(back, Radius);
                    world.Fx.Emit(FxType.Smoke, Position, Vector2.up, 1.4f, Color.white);
                    spent = true;
                    break;
                }
                case ArtId.Bell:
                    world.BronzeBell(Position);
                    spent = true;
                    break;
                case ArtId.Chain:
                    spent = world.FindAssistTarget(Position, aim, 40f, 7.5f, false) != null;
                    world.HookChain(this, aim);
                    break;
                case ArtId.Rupture: spent = DetonateSeals(); if (!spent) world.Fx.Popup(Strings.PopupNoSeals, Position + Vector2.up, pal.uiDim); break;
                case ArtId.Soul: spent = BecomeSoul(SoulAvailable); break;
                default: spent = false; break;
            }
            if (!spent) return;
            Build.ArtCharges--;
            run?.Profile.OnTool();
            world.HintOnce(HintId.Tools);
        }

        /// <summary>Kiai como Arte: interrompe o golpe vermelho em preparo do inimigo mais perto.</summary>
        bool ArtKiai()
        {
            if (kiaiCooldown > 0) return false;
            kiaiCooldown = 20;
            world.Fx.Emit(FxType.Kiai, Position, Facing, KiaiRange, pal.gold);
            world.RegisterKiai(this);
            Enemy best = null;
            float bestDist = float.MaxValue;
            foreach (Enemy e in world.Enemies)
            {
                AttackDef a = e.CurrentAttack;
                if (a == null || !a.IsDangerous) continue;
                int until = e.FramesUntilActive;
                if (until > 18 || until < -2) continue;
                float d = Vector2.Distance(e.Position, Position);
                if (d > KiaiRange + e.Radius || d >= bestDist) continue;
                best = e;
                bestDist = d;
            }
            if (best == null)
            {
                world.Fx.Popup(Strings.PopupKiaiNothing, Position + Vector2.up * 1.1f, pal.uiDim);
                return false;
            }
            best.Interrupt(20f, 45);
            world.Fx.Popup(Strings.PopupKiai, Position + Vector2.up * 1.3f, pal.gold);
            world.RegisterPerfect(Position);
            world.HintOnce(HintId.Kiai);
            return true;
        }

        /// <summary>Cada finalização devolve uma carga da Arte.</summary>
        void RechargeArt()
        {
            if (Build == null || Build.ArtCharges >= Arts.MaxCharges) return;
            Build.ArtCharges++;
            world.Fx.Popup(string.Format(Strings.PopupArtCharge, Arts.Name(Build.Art)), Position + Vector2.up * 1.6f, pal.gold);
        }
    }
}
