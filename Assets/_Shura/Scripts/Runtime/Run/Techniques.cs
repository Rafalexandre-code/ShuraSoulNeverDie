using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>The marks (kanji) a technique carries (design doc §4.4).</summary>
    public enum Mark
    {
        Corte,
        Guarda,
        Sombra,
        Espirito,
        Sangue,
        Fogo,
        Vento,
        Trovao,
    }

    public static class Marks
    {
        public static string Kanji(Mark m)
        {
            switch (m)
            {
                case Mark.Corte: return "斬";
                case Mark.Guarda: return "守";
                case Mark.Sombra: return "影";
                case Mark.Espirito: return "霊";
                case Mark.Sangue: return "血";
                case Mark.Fogo: return "火";
                case Mark.Vento: return "風";
                default: return "雷";
            }
        }

        public static string Name(Mark m)
        {
            switch (m)
            {
                case Mark.Corte: return "Corte";
                case Mark.Guarda: return "Guarda";
                case Mark.Sombra: return "Sombra";
                case Mark.Espirito: return "Espírito";
                case Mark.Sangue: return "Sangue";
                case Mark.Fogo: return "Fogo";
                case Mark.Vento: return "Vento";
                default: return "Trovão";
            }
        }

        public static Color Color(Mark m)
        {
            switch (m)
            {
                case Mark.Corte: return ColorUtil.Hex("#E8E8F0");
                case Mark.Guarda: return ColorUtil.Hex("#7FB2FF");
                case Mark.Sombra: return Elements.Color(Element.Shadow);
                case Mark.Espirito: return ColorUtil.Hex("#B07CFF");
                case Mark.Sangue: return Elements.Color(Element.Blood);
                case Mark.Fogo: return Elements.Color(Element.Fire);
                case Mark.Vento: return Elements.Color(Element.Wind);
                default: return Elements.Color(Element.Lightning);
            }
        }

        /// <summary>"斬 Corte · 守 Guarda".</summary>
        public static string Label(Mark[] marks)
        {
            var sb = new StringBuilder();
            foreach (Mark m in marks)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(Kanji(m)).Append(' ').Append(Name(m));
            }
            return sb.ToString();
        }

        /// <summary>What each level above I adds, per mark.</summary>
        public static void ApplyLevel(Mark m, PlayerModifiers mods, int steps)
        {
            if (steps <= 0) return;
            switch (m)
            {
                case Mark.Corte: mods.damageMult *= 1f + 0.05f * steps; break;
                case Mark.Guarda: mods.maxPostureBonus += 10f * steps; break;
                case Mark.Sombra: mods.dodgeDistanceMult *= 1f + 0.06f * steps; break;
                case Mark.Espirito: mods.flowGainMult *= 1f + 0.1f * steps; break;
                case Mark.Sangue: mods.lifesteal += 0.015f * steps; break;
                case Mark.Fogo: mods.critChance += 0.03f * steps; break;
                case Mark.Vento: mods.moveSpeedMult *= 1f + 0.04f * steps; break;
                default: mods.dodgeRechargeMult *= 1f - 0.06f * steps; break;
            }
        }

        public static string LevelText(Mark m)
        {
            switch (m)
            {
                case Mark.Corte: return "+5% de dano";
                case Mark.Guarda: return "+10 de postura máxima";
                case Mark.Sombra: return "+6% de distância de esquiva";
                case Mark.Espirito: return "+10% de ganho de Fluxo";
                case Mark.Sangue: return "+1,5% de roubo de vida";
                case Mark.Fogo: return "+3% de chance de crítico";
                case Mark.Vento: return "+4% de velocidade";
                default: return "esquivas recarregam 6% mais rápido";
            }
        }

        public static string EchoText(Mark m)
        {
            switch (m)
            {
                case Mark.Corte: return "um corte de vento parte rumo ao alvo";
                case Mark.Guarda: return "você fica intocável por 0,4 s";
                case Mark.Sombra: return "uma sombra explode no alvo";
                case Mark.Espirito: return "um fogo-fátuo voa até o inimigo mais próximo";
                case Mark.Sangue: return "o alvo sangra e você cura 2";
                case Mark.Fogo: return "o alvo queima";
                case Mark.Vento: return "uma lua de vento corta tudo à frente";
                default: return "um raio salta entre os inimigos";
            }
        }

        /// <summary>The echo of a mark (level III, Eco branch): where the technique acted.</summary>
        public static void Echo(World world, Mark m, Vector2 at, Enemy target)
        {
            PlayerController p = world.Player;
            if (p == null || !p.IsAlive) return;
            if (target == null || !target.CanBeHit || target.IsAlly) target = world.FindNearestEnemy(at, null);
            Vector2 dir = target != null ? target.Position - p.Position : p.Facing;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : p.Facing;
            Color c = Color(m);
            switch (m)
            {
                case Mark.Corte:
                    world.PlayerInstantStrike(p.Position, dir, HitShape.Line(3.6f, 0.7f), 9f, 14f);
                    world.Fx.Emit(FxType.Elo, p.Position, dir, 1f, c);
                    break;
                case Mark.Guarda:
                    p.GrantInvulnerable(24);
                    world.Fx.Emit(FxType.Phase, p.Position, Vector2.up, p.Radius, c);
                    break;
                case Mark.Sombra:
                    if (target == null) return;
                    target.MarkElement(Element.Shadow);
                    world.ScheduleBurst(target.Position, 10, 1.3f, 8f, 16f, c);
                    break;
                case Mark.Espirito:
                    if (target == null) return;
                    world.FireWisp(at, target.Position - at, 9f, 12f);
                    break;
                case Mark.Sangue:
                    if (target != null) target.ApplyDot(5f, 3f, Element.Blood);
                    p.Heal(2f);
                    break;
                case Mark.Fogo:
                    if (target != null) target.ApplyDot(6f, 3f, Element.Fire);
                    else world.ScheduleBurst(at, 6, 1.2f, 6f, 8f, c);
                    break;
                case Mark.Vento:
                    world.FireSlashWave(p.Position, dir, 7f, 10f);
                    break;
                default:
                    if (target != null) world.ChainLightning(target, 2, 8f, 10f);
                    break;
            }
        }
    }

    /// <summary>
    /// A technique absorbed from an enemy. Absorbing the same enemy again raises it (I → II → III):
    /// level II drops the price and strengthens its marks; level III asks for a branch —
    /// Forma Plena (its numbers 50% stronger) or Eco (every time it acts, its marks answer).
    /// </summary>
    public abstract class TechniqueDef : RewardDef
    {
        public const int MaxLevel = 3;
        public const int BranchFull = 1;
        public const int BranchEcho = 2;

        public int Level = 1;
        /// <summary>0 before level III; then <see cref="BranchFull"/> or <see cref="BranchEcho"/>.</summary>
        public int Branch;
        float lastEcho = -99f;

        public sealed override RewardKind Kind => RewardKind.Technique;
        public abstract Mark[] MarkSet { get; }
        protected abstract string BaseName { get; }
        protected abstract string BaseGain { get; }
        protected abstract string BaseCost { get; }

        public static string Numeral(int level) => level >= 3 ? "III" : level == 2 ? "II" : "I";

        public string Title => BaseName;

        public sealed override string Name => Level <= 1 ? BaseName : BaseName + " " + Numeral(Level);

        public sealed override string Gain
        {
            get
            {
                string g = BaseGain;
                if (Level >= 3) g += Branch == BranchFull ? " Forma Plena: 50% mais forte." : " Eco: " + EchoSummary(MarkSet) + ".";
                return g;
            }
        }

        public sealed override string Cost => Level >= 2 ? Strings.TechniqueNoCost : BaseCost;

        /// <summary>The price is only paid at level I.</summary>
        protected bool PaysCost => Level < 2;

        /// <summary>The technique's own numbers: ×1.5 in the Forma Plena branch.</summary>
        protected float Power => Level >= 3 && Branch == BranchFull ? 1.5f : 1f;

        public sealed override void Apply(PlayerModifiers m)
        {
            ApplyGain(m);
            if (PaysCost) ApplyCost(m);
            foreach (Mark mark in MarkSet) Marks.ApplyLevel(mark, m, Level - 1);
        }

        protected virtual void ApplyGain(PlayerModifiers m) { }
        protected virtual void ApplyCost(PlayerModifiers m) { }

        /// <summary>The technique just did its thing: in the Eco branch, its marks answer (at most every 0.4 s).</summary>
        protected void Acted(World world, Vector2 at, Enemy target)
        {
            if (Level < 3 || Branch != BranchEcho || world.Run == null) return;
            if (world.Run.Elapsed - lastEcho < 0.4f) return;
            lastEcho = world.Run.Elapsed;
            foreach (Mark m in MarkSet) Marks.Echo(world, m, at, target);
        }

        public static string EchoSummary(Mark[] marks)
        {
            var sb = new StringBuilder();
            foreach (Mark m in marks)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(Marks.Kanji(m)).Append(' ').Append(Marks.EchoText(m));
            }
            return sb.ToString();
        }

        public static string LevelSummary(Mark[] marks)
        {
            var sb = new StringBuilder();
            foreach (Mark m in marks)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Marks.Kanji(m)).Append(' ').Append(Marks.LevelText(m));
            }
            return sb.ToString();
        }
    }

    /// <summary>The reward card that raises a technique already owned (and picks its branch at III).</summary>
    public sealed class TechniqueLevelUp : RewardDef
    {
        public readonly TechniqueDef Target;
        public readonly int ToLevel;
        public readonly int ChosenBranch;

        public TechniqueLevelUp(TechniqueDef target, int toLevel, int branch)
        {
            Target = target;
            ToLevel = toLevel;
            ChosenBranch = branch;
        }

        public override string Id => Target.Id + "#" + ToLevel + "." + ChosenBranch;
        public override RewardKind Kind => RewardKind.Technique;
        public override string SourceEnemy => Target.SourceEnemy;

        public override string Name
        {
            get
            {
                string n = Target.Title + " " + TechniqueDef.Numeral(ToLevel);
                if (ToLevel >= 3) n += ChosenBranch == TechniqueDef.BranchFull ? " · Forma Plena" : " · Eco";
                return n;
            }
        }

        public override string Gain
        {
            get
            {
                if (ToLevel == 2) return string.Format(Strings.TechniqueLevel2, TechniqueDef.LevelSummary(Target.MarkSet));
                return ChosenBranch == TechniqueDef.BranchFull
                    ? string.Format(Strings.TechniqueBranchFull, TechniqueDef.LevelSummary(Target.MarkSet))
                    : string.Format(Strings.TechniqueBranchEcho, TechniqueDef.EchoSummary(Target.MarkSet));
            }
        }

        public override string Cost => ToLevel >= 3 ? Strings.TechniqueBranchCost : Strings.TechniqueLevelCost;
    }

    // ── Fusions ────────────────────────────────────────────────────────────────────────

    public static class Fusions
    {
        public const string BloodMoon = "fus_lua_sangrenta";
        public const string TwinCrow = "fus_corvo_duplo";
        public const string StormBell = "fus_sino_tempestuoso";
        public const string OniFuse = "fus_pavio_do_oni";
        public const string GhostLantern = "fus_lanterna_fantasma";
        public const string LivingWall = "fus_muralha_viva";
        public const string ThunderEye = "fus_olho_do_trovao";
        public const string HungryCurrent = "fus_correnteza_faminta";

        public static readonly string[] All = { BloodMoon, TwinCrow, StormBell, OniFuse, GhostLantern, LivingWall, ThunderEye, HungryCurrent };

        /// <summary>The two techniques each fusion asks for.</summary>
        public static void Parts(string id, out string a, out string b)
        {
            switch (id)
            {
                case BloodMoon: a = RewardIds.BloodThirst; b = RewardIds.PatientBlade; return;
                case TwinCrow: a = RewardIds.Ambush; b = RewardIds.ShadowCopy; return;
                case StormBell: a = RewardIds.PhantomStep; b = RewardIds.DuelistRhythm; return;
                case OniFuse: a = RewardIds.ShortFuse; b = RewardIds.OniCrush; return;
                case GhostLantern: a = RewardIds.LivingLantern; b = RewardIds.EtherealBody; return;
                case LivingWall: a = RewardIds.Bulwark; b = RewardIds.StubbornBones; return;
                case ThunderEye: a = RewardIds.SentinelBlink; b = RewardIds.ShadowCopy; return;
                default: a = RewardIds.KappaCurrent; b = RewardIds.SlimeHunger; return;
            }
        }

        public static bool Unlocked(PlayerBuild b, string id)
        {
            Parts(id, out string x, out string y);
            return b.Owns(x) && b.Owns(y) && !b.Owns(id);
        }

        public static List<string> Available(PlayerBuild b)
        {
            var list = new List<string>();
            foreach (string id in All)
            {
                if (Unlocked(b, id)) list.Add(id);
            }
            return list;
        }

        public static RewardDef Create(string id)
        {
            switch (id)
            {
                case BloodMoon: return new BloodMoonFusion();
                case TwinCrow: return new TwinCrowFusion();
                case StormBell: return new StormBellFusion();
                case OniFuse: return new OniFuseFusion();
                case GhostLantern: return new GhostLanternFusion();
                case LivingWall: return new LivingWallFusion();
                case ThunderEye: return new ThunderEyeFusion();
                case HungryCurrent: return new HungryCurrentFusion();
                default: return null;
            }
        }
    }

    /// <summary>A fused technique: born of two techniques already owned, no price.</summary>
    public abstract class FusionDef : RewardDef
    {
        public override RewardKind Kind => RewardKind.Technique;
        public override string Cost => Strings.FusionCost;
        public abstract Mark[] MarkSet { get; }

        public string Recipe
        {
            get
            {
                Fusions.Parts(Id, out string a, out string b);
                RewardDef x = RewardRegistry.Create(a), y = RewardRegistry.Create(b);
                return (x != null ? StripLevel(x) : a) + " + " + (y != null ? StripLevel(y) : b);
            }
        }

        static string StripLevel(RewardDef r) => r is TechniqueDef t ? t.Title : r.Name;

        public override string SourceEnemy => string.Format(Strings.FusionOf, Recipe);
    }

    /// <summary>Lua Sangrenta: the blow held after a perfect parry drains life all around.</summary>
    public sealed class BloodMoonFusion : FusionDef
    {
        float armedUntil = -1f;
        public override string Id => Fusions.BloodMoon;
        public override string Name => "Lua Sangrenta";
        public override string Gain => "Após um parry perfeito, seu próximo golpe em 1 s drena todos ao redor (raio 2,6: 10 dano cada, e você cura 3 por inimigo).";
        public override Mark[] MarkSet => new[] { Mark.Sangue, Mark.Corte };

        public override void OnPerfectParry(World world, Enemy attacker) => armedUntil = world.Run.Elapsed + 1f;

        public override void OnDealtDamage(World world, Enemy target, float damage)
        {
            if (world.Run.Elapsed > armedUntil) return;
            armedUntil = -1f;
            PlayerController p = world.Player;
            int n = 0;
            foreach (Enemy e in world.Enemies)
            {
                if (!e.CanBeHit || e.IsAlly || Vector2.Distance(e.Position, p.Position) > 2.6f) continue;
                e.ReceivePlayerHit(10f, 6f, (e.Position - p.Position).normalized, true);
                world.Fx.Emit(FxType.Drain, p.Position, (p.Position - e.Position).normalized, Vector2.Distance(e.Position, p.Position), Elements.Color(Element.Blood));
                n++;
            }
            if (n > 0) p.Heal(3f * n);
            world.Fx.Popup("LUA SANGRENTA", p.Position + Vector2.up * 1.2f, Elements.Color(Element.Blood));
            world.Fx.Emit(FxType.Burst, p.Position, Vector2.up, 2.6f, Elements.Color(Element.Blood));
        }
    }

    /// <summary>Corvo Duplo: a perfect dodge leaves a shadow that repeats your blow.</summary>
    public sealed class TwinCrowFusion : FusionDef
    {
        public override string Id => Fusions.TwinCrow;
        public override string Name => "Corvo Duplo";
        public override string Gain => "Toda esquiva perfeita deixa uma cópia que repete seu golpe 0,4 s depois (14 dano, 16 postura à frente).";
        public override Mark[] MarkSet => new[] { Mark.Sombra, Mark.Trovao };

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            PlayerController p = world.Player;
            Vector2 at = attacker != null && !attacker.IsGone ? attacker.Position : p.Position + p.Facing * 1.2f;
            world.ScheduleBurst(at, 24, 1.5f, 14f, 16f, Elements.Color(Element.Shadow));
            world.Fx.Emit(FxType.Phase, p.Position, Vector2.up, p.Radius * 1.4f, Elements.Color(Element.Shadow));
            world.Fx.Popup("CORVO DUPLO", p.Position + Vector2.up * 1.1f, Elements.Color(Element.Shadow));
        }
    }

    /// <summary>Sino Tempestuoso: every perfect dodge rings a bell that cuts all around.</summary>
    public sealed class StormBellFusion : FusionDef
    {
        public override string Id => Fusions.StormBell;
        public override string Name => "Sino Tempestuoso";
        public override string Gain => "Cada esquiva perfeita toca um sino: dano em círculo ao seu redor (raio 2,8: 10 dano, 20 postura).";
        public override Mark[] MarkSet => new[] { Mark.Espirito, Mark.Trovao };

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            PlayerController p = world.Player;
            Color gold = world.Config.palette.gold;
            world.ScheduleBurst(p.Position, 2, 2.8f, 10f, 20f, gold);
            world.Fx.Emit(FxType.Elo, p.Position, Vector2.up, 2f, gold);
            world.Fx.Popup("SINO", p.Position + Vector2.up * 1.2f, gold);
        }
    }

    /// <summary>Pavio do Oni: every dash leaves a row of bombs along its path.</summary>
    public sealed class OniFuseFusion : FusionDef
    {
        float last = -99f;
        public override string Id => Fusions.OniFuse;
        public override string Name => "Pavio do Oni";
        public override string Gain => "Toda esquiva deixa 3 bombas no caminho (9 dano, 14 postura cada). Uma vez a cada 1,2 s.";
        public override Mark[] MarkSet => new[] { Mark.Fogo, Mark.Guarda };

        public override void OnDodge(World world, Vector2 from)
        {
            if (world.Run.Elapsed - last < 1.2f) return;
            last = world.Run.Elapsed;
            Vector2 dir = world.Player.Facing;
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = world.Layout != null ? world.Layout.NearestFloor(from + dir * (0.9f * i), 0.3f) : from;
                world.ScheduleBurst(at, 34 + 8 * i, 1.3f, 9f, 14f, World.BlastColor(BlastKind.Bomb), true);
            }
        }
    }

    /// <summary>Lanterna Fantasma: whoever falls rises as a will-o'-wisp that flies at its friends.</summary>
    public sealed class GhostLanternFusion : FusionDef
    {
        public override string Id => Fusions.GhostLantern;
        public override string Name => "Lanterna Fantasma";
        public override string Gain => "Inimigos mortos viram fogos-fátuos aliados que voam no inimigo mais próximo (12 dano, 14 postura).";
        public override Mark[] MarkSet => new[] { Mark.Espirito, Mark.Fogo };

        public override void OnKill(World world, Enemy enemy)
        {
            Enemy target = world.FindNearestEnemy(enemy.Position, enemy);
            if (target == null) return;
            world.FireWisp(enemy.Position, target.Position - enemy.Position, 12f, 14f);
        }
    }

    /// <summary>Muralha Viva: once per room, falling puts you back together at 30% health.</summary>
    public sealed class LivingWallFusion : FusionDef
    {
        int usedInRoom = -1;
        public override string Id => Fusions.LivingWall;
        public override string Name => "Muralha Viva";
        public override string Gain => "Uma vez por sala, morrer te remonta com 30% da vida.";
        public override Mark[] MarkSet => new[] { Mark.Guarda, Mark.Espirito };

        public override bool OnLethalHit(World world)
        {
            if (world.Run == null || usedInRoom == world.Run.RoomIndex) return false;
            usedInRoom = world.Run.RoomIndex;
            PlayerController p = world.Player;
            if (p.Build != null) p.Build.PendingReviveHealth = Mathf.Max(p.Build.PendingReviveHealth, p.MaxHealth * 0.3f);
            p.GrantInvulnerable(60);
            world.Fx.Popup("MURALHA VIVA", p.Position + Vector2.up * 1.2f, ColorUtil.Hex("#7FB2FF"));
            world.Fx.Emit(FxType.Spawn, p.Position, Vector2.up, p.Radius * 1.6f, ColorUtil.Hex("#7FB2FF"));
            return true;
        }
    }

    /// <summary>Olho do Trovão: the blink leaves a bolt where you stood.</summary>
    public sealed class ThunderEyeFusion : FusionDef
    {
        Vector2 from;
        public override string Id => Fusions.ThunderEye;
        public override string Name => "Olho do Trovão";
        public override string Gain => "Na esquiva perfeita, um raio cai onde você estava (raio 1,8: 14 dano, 20 postura).";
        public override Mark[] MarkSet => new[] { Mark.Trovao, Mark.Sombra };

        public override void OnDodge(World world, Vector2 at) => from = at;

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            Color c = Elements.Color(Element.Lightning);
            world.ScheduleBurst(from, 8, 1.8f, 14f, 20f, c);
            world.Fx.Emit(FxType.Lightning, from + Vector2.up * 4f, Vector2.down, 4f, c);
        }
    }

    /// <summary>Correnteza Faminta: rising from the ground splits the one it hits into two weak slimes.</summary>
    public sealed class HungryCurrentFusion : FusionDef
    {
        float last = -99f;
        public override string Id => Fusions.HungryCurrent;
        public override string Name => "Correnteza Faminta";
        public override string Gain => "Esquiva perfeita contra um inimigo comum o divide em dois lacaios fracos. Uma vez a cada 4 s.";
        public override Mark[] MarkSet => new[] { Mark.Vento, Mark.Sangue };

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            if (attacker == null || attacker.IsGone || attacker.IsAlly || attacker.IsElite || attacker.Def.minion) return;
            if (world.IsBossLike(attacker) || world.Run.Elapsed - last < 4f) return;
            last = world.Run.Elapsed;
            world.Divide(attacker, EnemyDefs.LodoPequeno, 2);
            world.Fx.Popup("CORRENTEZA FAMINTA", attacker.Position + Vector2.up * 1.2f, Elements.Color(Element.Wind));
        }
    }
}
