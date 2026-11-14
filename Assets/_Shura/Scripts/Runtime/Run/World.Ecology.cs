using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Phase 5, part 3 in the arena: the ecology of the four factions (§5.3) and the
    /// formations that fight as one (§5.4).
    /// </summary>
    public sealed partial class World
    {
        struct Possession
        {
            public EnemyDef def;
            public Vector2 at;
            public Enemy onryo;
            public int frames;
        }

        sealed class ActiveFormation
        {
            public FormationDef def;
            public readonly List<Enemy> members = new List<Enemy>();
            public int cooldown;
            public Enemy.State lastLeadState;
        }

        readonly List<Possession> possessions = new List<Possession>();
        readonly List<ActiveFormation> formations = new List<ActiveFormation>();
        EnemyDef lastFallen;
        int ecologyTick;

        void ClearEcology()
        {
            possessions.Clear();
            formations.Clear();
            ecologyTick = 0;
        }

        /// <summary>A body falls: the Onryō may raise a human, and the faceless take its moves.</summary>
        void OnEcologyDeath(Enemy enemy)
        {
            if (enemy.IsAlly || enemy.Def.minion || enemy.IsBoss) return;
            if (!enemy.Def.mimic) lastFallen = enemy.Def;
            foreach (Enemy e in Enemies)
            {
                if (e != enemy && !e.IsGone && e.Def.mimic) e.Mimic(enemy.Def);
            }
            if (Factions.Of(enemy.Def) != EnemyFaction.Human) return;
            foreach (Enemy e in Enemies)
            {
                if (e.IsGone || e.Def.id != "onryo" || Vector2.Distance(e.Position, enemy.Position) > 8f) continue;
                possessions.Add(new Possession { def = enemy.Def, at = enemy.Position, onryo = e, frames = 50 });
                return;
            }
        }

        /// <summary>The Nopperabō arrives wearing the last fallen's moves; the mirror, a duelist's.</summary>
        void OnEcologySpawn(Enemy e)
        {
            if (e.Def.mimic && lastFallen != null) e.Mimic(lastFallen);
            if (e.Def.mirror) e.MakeMirror(Host);
        }

        void TickEcology()
        {
            ecologyTick++;
            for (int i = possessions.Count - 1; i >= 0; i--)
            {
                Possession p = possessions[i];
                if (p.onryo == null || p.onryo.IsGone)
                {
                    possessions.RemoveAt(i);
                    continue;
                }
                if (--p.frames > 0)
                {
                    possessions[i] = p;
                    continue;
                }
                possessions.RemoveAt(i);
                Enemy raised = CreateEnemy(Factions.PossessedOf(p.def), Layout.NearestFloor(p.at, p.def.radius), false, p.onryo);
                Fx.Emit(FxType.Smoke, p.at, Vector2.up, 1.4f, ColorUtil.Hex("#B98CFF"));
                Fx.Popup(Strings.PopupPossessed, p.at + Vector2.up * 1.3f, ColorUtil.Hex("#B98CFF"));
                HintOnce(HintId.Factions);
                raised.Target = p.onryo.Target;
            }

            // The paper lantern draws will-o'-wisps that burst on the Oni.
            if (ecologyTick % 360 == 180)
            {
                foreach (Enemy lantern in Enemies)
                {
                    if (lantern.IsGone || lantern.Def.id != "lanterna") continue;
                    Enemy oni = null;
                    float best = 14f;
                    foreach (Enemy e in Enemies)
                    {
                        if (e.IsGone || Factions.Of(e.Def) != EnemyFaction.Oni) continue;
                        float d = Vector2.Distance(e.Position, lantern.Position);
                        if (d < best)
                        {
                            best = d;
                            oni = e;
                        }
                    }
                    if (oni == null) continue;
                    ScheduleEnemyBlast(lantern, oni.Position, 1.1f, 55, 14f, 18f, BlastKind.Fire, true);
                    Fx.Popup(Strings.PopupWisp, oni.Position + Vector2.up * 1.4f, ColorUtil.Hex("#7FD3FF"));
                }
            }
            TickFormations();
        }

        // ── Formations (§5.4) ────────────────────────────────────────────────────────

        void DetectFormation(List<Enemy> wave)
        {
            FormationDef f = Formations.Detect(wave);
            if (f == null) return;
            var active = new ActiveFormation { def = f };
            active.members.AddRange(wave);
            formations.Add(active);
            Fx.Toast(string.Format(Strings.ToastFormation, f.name), Config.palette.danger);
            Fx.Toast(f.hint, Config.palette.uiText);
            HintOnce(HintId.Formation);
        }

        Enemy Member(ActiveFormation a, string id)
        {
            foreach (Enemy e in a.members)
            {
                if (e.Def.id == id && !e.IsGone) return e;
            }
            return null;
        }

        void TickFormations()
        {
            for (int i = formations.Count - 1; i >= 0; i--)
            {
                ActiveFormation a = formations[i];
                if (a.cooldown > 0) a.cooldown--;
                switch (a.def.id)
                {
                    case Formations.Phalanx:
                    {
                        Enemy shield = Member(a, "escudeiro");
                        foreach (Enemy e in a.members)
                        {
                            if (e.Def.id != "lanceiro") continue;
                            e.FormationGuarded = shield != null && !e.IsGone && Vector2.Distance(e.Position, shield.Position) < 3.2f;
                        }
                        if (shield == null)
                        {
                            Fx.Toast(Strings.ToastPhalanxBroken, Config.palette.gold);
                            formations.RemoveAt(i);
                        }
                        break;
                    }
                    case Formations.Ritual:
                    {
                        Enemy caster = Member(a, "onmyoji"), healer = Member(a, "curandeira");
                        if (caster == null || healer == null)
                        {
                            formations.RemoveAt(i);
                            break;
                        }
                        // Interrupting one stops both.
                        bool broken = Shaken(caster) || Shaken(healer);
                        if (broken && a.cooldown == 0)
                        {
                            caster.Stun(70);
                            healer.Stun(70);
                            a.cooldown = 240;
                            Fx.Toast(Strings.ToastRitualBroken, Config.palette.gold);
                        }
                        else if (ecologyTick % 300 == 0 && Vector2.Distance(caster.Position, healer.Position) < 8f) caster.Provoke();
                        break;
                    }
                    case Formations.Hunt:
                    {
                        Enemy kunoichi = Member(a, "kunoichi"), shadow = Member(a, "sombra");
                        if (kunoichi == null || shadow == null)
                        {
                            formations.RemoveAt(i);
                            break;
                        }
                        // When the kunoichi strikes, the shadow appears at your back.
                        if (kunoichi.CurrentState == Enemy.State.Attacking && a.lastLeadState != Enemy.State.Attacking && a.cooldown == 0)
                        {
                            PlayerController t = kunoichi.Target ?? Host;
                            if (t != null) shadow.AppearBehind(t);
                            a.cooldown = 300;
                        }
                        a.lastLeadState = kunoichi.CurrentState;
                        break;
                    }
                    case Formations.Bridge:
                    case Formations.Storm:
                        bool any = false;
                        foreach (Enemy e in a.members) any |= !e.IsGone;
                        if (!any) formations.RemoveAt(i);
                        break;
                }
            }
        }

        static bool Shaken(Enemy e) =>
            e.CurrentState == Enemy.State.Stagger || e.CurrentState == Enemy.State.Broken || e.CurrentState == Enemy.State.Recoil;

        bool InFormation(string formation, Enemy e)
        {
            foreach (ActiveFormation a in formations)
            {
                if (a.def.id == formation && a.members.Contains(e)) return true;
            }
            return false;
        }

        /// <summary>The Bridge: the executioner's chain closed — the archer shoots now.</summary>
        void OnFormationBlowLanded(Enemy attacker, AttackDef attack)
        {
            if (attacker.Def.id != "carrasco" || attack.kind != AttackKind.Grab || !InFormation(Formations.Bridge, attacker)) return;
            foreach (ActiveFormation a in formations)
            {
                if (a.def.id != Formations.Bridge || !a.members.Contains(attacker)) continue;
                Enemy archer = Member(a, "arqueiro");
                archer?.Provoke();
            }
        }

        /// <summary>The Storm: a thunder copy turns the Watcher's bolt toward the nearest duelist.</summary>
        void RedirectStormBolt(Projectile p)
        {
            if (p.Redirected || p.Owner == null || p.Owner.Def.id != "olho" || !InFormation(Formations.Storm, p.Owner)) return;
            foreach (Enemy e in Enemies)
            {
                if (e.IsGone || (e.Def.id != "raijin_sombra" && e.Def.id != "raijin") || Vector2.Distance(e.Position, p.Position) > 1.6f) continue;
                PlayerController t = NearestAlivePlayer(p.Position);
                if (t == null) return;
                p.Direction = (t.Position - p.Position).normalized;
                p.Speed *= 1.3f;
                p.Redirected = true;
                Fx.Emit(FxType.Lightning, p.Position, p.Direction, 1.2f, ColorUtil.Hex("#9FE7FF"));
                return;
            }
        }

        // ── Webs (the Lady Spider and her daughters) ────────────────────────────────

        /// <summary>A web on the floor for a few seconds (the daughters' webs fade; the Lady's stay).</summary>
        public void AddWeb(Vector2 at, float seconds)
        {
            if (arenaRoot == null) return;
            SpriteRenderer view = SpriteUtil.Make("Web", bossProps != null ? bossProps : arenaRoot, WebSprite, Color.white, Sorting.Ground + 5);
            view.transform.position = at;
            webs.Add(new WebSpot { at = at, until = Run != null ? Run.Elapsed + seconds : float.MaxValue, view = view });
        }

        void TickWebs()
        {
            if (Run == null) return;
            for (int i = webs.Count - 1; i >= 0; i--)
            {
                if (Run.Elapsed < webs[i].until) continue;
                if (webs[i].view != null) Object.Destroy(webs[i].view.gameObject);
                webs.RemoveAt(i);
            }
        }
    }
}
