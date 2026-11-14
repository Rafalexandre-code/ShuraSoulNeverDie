using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>What a duelist's blades make of them in a party (design doc §9.2).</summary>
    public enum PartyRole
    {
        None,
        /// <summary>Montante or Kanabō: draws the big ones (threat ×1.6).</summary>
        Vanguard,
        /// <summary>Tessen: a perfect parry mends the posture of the allies around.</summary>
        Guardian,
        /// <summary>Wakizashi: +30% on whoever is busy with someone else.</summary>
        Hunter,
        /// <summary>Shakujō: a perfect parry breaks seals on allies and makes spirits solid.</summary>
        Exorcist,
    }

    /// <summary>
    /// Playing together, not side by side (§9.2–9.4): threat decides whom the enemies hunt,
    /// roles grow from the blades, the fallen still cry out as spirits and can be bought back
    /// with a technique, and a Blood Pact binds two duelists' Flow and Shura.
    /// </summary>
    public sealed partial class World
    {
        public const float ThreatDecayPerSecond = 0.2f;
        public const float ThreatPerMetre = 25f;
        public const int SpiritKiaiCooldown = 600;
        public const float SpiritKiaiRange = 3f;
        public const float RescuePerParry = 0.4f;
        public const int FrenzyFrames = 600;
        public const float FrenzyDamage = 1.5f;
        public const float HunterDamage = 1.3f;
        public const float GuardianMend = 15f;

        readonly Dictionary<PlayerController, float> threat = new Dictionary<PlayerController, float>();
        readonly Dictionary<PlayerController, PartyRole> roles = new Dictionary<PlayerController, PartyRole>();
        readonly Dictionary<PlayerController, int> spiritKiai = new Dictionary<PlayerController, int>();
        readonly Dictionary<PlayerController, int> frenzy = new Dictionary<PlayerController, int>();
        readonly Dictionary<PlayerController, PlayerController> pactOf = new Dictionary<PlayerController, PlayerController>();
        readonly Dictionary<PlayerController, float> pactFlow = new Dictionary<PlayerController, float>();
        readonly Dictionary<PlayerController, float> pactShura = new Dictionary<PlayerController, float>();
        readonly HashSet<PlayerController> wasAlive = new HashSet<PlayerController>();

        public bool InParty => Players.Count >= 2;

        // ── Threat and roles ─────────────────────────────────────────────────────────

        public float ThreatOf(PlayerController p) => p != null && threat.TryGetValue(p, out float t) ? t : 0f;

        public PartyRole RoleOf(PlayerController p) => p != null && roles.TryGetValue(p, out PartyRole r) ? r : PartyRole.None;

        /// <summary>Damage, perfect parries and provocation draw the enemies; the Vanguard and a high guard draw more.</summary>
        public void AddThreat(PlayerController p, float amount)
        {
            if (p == null || amount <= 0f || !InParty) return;
            float mult = 1f;
            if (RoleOf(p) == PartyRole.Vanguard) mult *= 1.6f;
            if (p.Stance == Kamae.Jodan) mult *= 1.6f;
            threat[p] = ThreatOf(p) + amount * mult;
        }

        public static PartyRole RoleFor(WeaponDef[] weapons)
        {
            if (weapons == null) return PartyRole.None;
            foreach (WeaponDef w in weapons)
            {
                if (w == null) continue;
                switch (w.baseId ?? w.id)
                {
                    case "montante":
                    case "kanabo": return PartyRole.Vanguard;
                    case "tessen": return PartyRole.Guardian;
                    case "wakizashi": return PartyRole.Hunter;
                    case "shakujo": return PartyRole.Exorcist;
                }
            }
            return PartyRole.None;
        }

        /// <summary>A new room: threat starts from nothing, and each duelist's blades give the role.</summary>
        void AssignRoles()
        {
            threat.Clear();
            roles.Clear();
            frenzy.Clear();
            wasAlive.Clear();
            if (!InParty) return;
            foreach (PlayerController p in Players)
            {
                PartyRole r = RoleFor(p.Build?.Weapons);
                roles[p] = r;
                if (p.IsAlive) wasAlive.Add(p);
                if (r != PartyRole.None) Fx.Popup(Strings.RoleName(r), p.Position + Vector2.up * 1.5f, Config.palette.gold);
            }
            if (Depth >= 0 && !TutorialRoom) HintOnce(HintId.Roles);
        }

        /// <summary>Enemies prefer whoever threatens them most (25 threat weigh like a metre of distance).</summary>
        float ThreatPull(PlayerController p) => InParty ? ThreatOf(p) / ThreatPerMetre : 0f;

        /// <summary>The Hunter's bonus on an enemy busy with someone else, and the frenzy of a Blood Pact.</summary>
        public float PartyDamageMult(PlayerController p, Enemy e)
        {
            if (p == null || !InParty) return 1f;
            float m = 1f;
            if (RoleOf(p) == PartyRole.Hunter && e != null && e.Target != null && e.Target != p && e.Target.IsAlive) m *= HunterDamage;
            if (frenzy.TryGetValue(p, out int f) && f > 0) m *= FrenzyDamage;
            return m;
        }

        public bool InFrenzy(PlayerController p) => p != null && frenzy.TryGetValue(p, out int f) && f > 0;

        /// <summary>A perfect parry in a party: threat, the role's gift, and a faster rescue for a fallen ally nearby.</summary>
        void OnPartyParry(PlayerController p, Enemy e)
        {
            if (!InParty || p == null) return;
            AddThreat(p, 30f);
            switch (RoleOf(p))
            {
                case PartyRole.Guardian:
                    foreach (PlayerController ally in Players)
                    {
                        if (ally == p || !ally.IsAlive || Vector2.Distance(ally.Position, p.Position) > 3.5f) continue;
                        ally.Posture.Recover(GuardianMend);
                        Fx.Emit(FxType.FlowUp, ally.Position, Vector2.up, 0.5f, Config.palette.gold);
                    }
                    break;
                case PartyRole.Exorcist:
                    foreach (PlayerController ally in Players)
                    {
                        if (ally.IsAlive && Vector2.Distance(ally.Position, p.Position) <= 4f) ally.ReleaseSeal();
                    }
                    foreach (Enemy x in Enemies)
                    {
                        if (!x.IsGone && Vector2.Distance(x.Position, p.Position) <= 3f) x.ForceExpose(180);
                    }
                    break;
            }
            // Parrying the blows that fall on a fallen friend brings them back sooner.
            foreach (PlayerController down in Players)
            {
                if (down == p || down.IsAlive || Vector2.Distance(down.Position, p.Position) > ReviveRange * 2f) continue;
                reviveProgress.TryGetValue(down, out float t);
                reviveProgress[down] = Mathf.Min(ReviveSeconds - SimClock.Dt, t + RescuePerParry);
                Fx.Popup(Strings.PopupRescueParry, down.Position + Vector2.up * 0.9f, Config.palette.gold);
            }
        }

        // ── The Blood Pact ───────────────────────────────────────────────────────────

        public void ClearPacts()
        {
            pactOf.Clear();
            pactFlow.Clear();
            pactShura.Clear();
        }

        /// <summary>Two duelists who sealed a pact in the lobby share Flow and Shura for the run.</summary>
        public void SealPact(PlayerController a, PlayerController b)
        {
            if (a == null || b == null || a == b || pactOf.ContainsKey(a) || pactOf.ContainsKey(b)) return;
            pactOf[a] = b;
            pactOf[b] = a;
        }

        public PlayerController PactOf(PlayerController p) => p != null && pactOf.TryGetValue(p, out PlayerController o) && Players.Contains(o) ? o : null;

        void TickPacts()
        {
            foreach (KeyValuePair<PlayerController, PlayerController> kv in pactOf)
            {
                PlayerController a = kv.Key, b = kv.Value;
                // Each pair once (the one with the lower index leads).
                if (Players.IndexOf(a) > Players.IndexOf(b) || !Players.Contains(a) || !Players.Contains(b)) continue;
                if (a.Build == null || b.Build == null) continue;
                SyncShared(a, b, pactFlow, x => x.Build.Flow.Points, (x, v) => x.Build.Flow.Set(v));
                if (!a.InAsura && !b.InAsura)
                {
                    SyncShared(a, b, pactShura, x => x.Build.Shura, (x, v) => x.Build.Shura = Mathf.Clamp(v, 0f, ShuraRules.Max));
                    if (a.Build.Shura >= ShuraRules.Max) a.AddShura(0.01f);
                    if (b.Build.Shura >= ShuraRules.Max) b.AddShura(0.01f);
                }
            }
        }

        /// <summary>One pool: whatever either gained or lost since the last tick, both now have.</summary>
        static void SyncShared(PlayerController a, PlayerController b, Dictionary<PlayerController, float> last,
                               System.Func<PlayerController, float> get, System.Action<PlayerController, float> set)
        {
            float va = get(a), vb = get(b);
            if (!last.TryGetValue(a, out float pool))
            {
                pool = Mathf.Max(va, vb);
            }
            else
            {
                last.TryGetValue(b, out float lb);
                pool += (va - pool) + (vb - lb);
            }
            set(a, pool);
            set(b, pool);
            pool = get(a);
            last[a] = pool;
            last[b] = get(b);
        }

        // ── Falling and being brought back ───────────────────────────────────────────

        /// <summary>
        /// Before the duelists act: a helper holding Interact over a fallen friend who presses the
        /// soul button offers their weakest technique and the friend stands at once.
        /// </summary>
        void TickOfferings()
        {
            if (!InParty) return;
            foreach (PlayerController p in Players)
            {
                if (!p.IsAlive || p.Input == null || !p.Input.InteractHeld || p.Build == null) continue;
                PlayerController down = FallenNear(p);
                if (down == null || !p.Input.Buffer.Peek(BufferedAction.Soul)) continue;
                p.Input.Buffer.Consume(BufferedAction.Soul);
                TechniqueDef offered = WeakestTechnique(p.Build);
                if (offered == null)
                {
                    Fx.Popup(Strings.PopupNoOffering, p.Position + Vector2.up, Config.palette.uiDim);
                    continue;
                }
                p.Build.Owned.Remove(offered);
                p.Build.RecomputeModifiers();
                p.ApplyStats(false);
                down.Revive(0.5f);
                reviveProgress.Remove(down);
                wasAlive.Add(down);
                Fx.Emit(FxType.TechniqueAbsorbed, down.Position, Vector2.up, 1f, Config.palette.gold);
                Fx.Toast(string.Format(Strings.ToastOffering, p.DisplayName, offered.Name, down.DisplayName), Config.palette.gold);
            }
        }

        public static TechniqueDef WeakestTechnique(PlayerBuild b)
        {
            TechniqueDef worst = null;
            foreach (RewardDef r in b.Owned)
            {
                if (r is TechniqueDef t && (worst == null || t.Level < worst.Level)) worst = t;
            }
            return worst;
        }

        /// <summary>After the duelists act: threat fades, spirits cry out, pacts share, a fall enrages the partner.</summary>
        void TickParty()
        {
            if (!InParty) return;
            float decay = Mathf.Pow(1f - ThreatDecayPerSecond, SimClock.Dt);
            foreach (PlayerController p in Players)
            {
                if (threat.TryGetValue(p, out float t)) threat[p] = t * decay;
                if (frenzy.TryGetValue(p, out int f) && f > 0) frenzy[p] = f - 1;
                if (spiritKiai.TryGetValue(p, out int k) && k > 0) spiritKiai[p] = k - 1;

                if (p.IsAlive)
                {
                    wasAlive.Add(p);
                    continue;
                }
                if (wasAlive.Remove(p)) OnPartnerFell(p);
                TickSpirit(p);
            }
            TickPacts();
        }

        void OnPartnerFell(PlayerController fallen)
        {
            threat.Remove(fallen);
            Fx.Popup(Strings.PopupSpiritHint, fallen.Position + Vector2.up * 1.6f, new Color(0.75f, 0.85f, 1f));
            PlayerController partner = PactOf(fallen);
            if (partner == null || !partner.IsAlive) return;
            frenzy[partner] = FrenzyFrames;
            Fx.Emit(FxType.ShuraRise, partner.Position, Vector2.up, 3f, Config.palette.danger);
            Fx.Popup(Strings.PopupFrenzy, partner.Position + Vector2.up * 1.4f, Config.palette.danger);
            Fx.Toast(string.Format(Strings.ToastFrenzy, partner.DisplayName, fallen.DisplayName), Config.palette.danger);
        }

        /// <summary>The fallen duelist is a spirit: once every ten seconds, a war cry that still staggers.</summary>
        void TickSpirit(PlayerController p)
        {
            if (p.Input == null || !p.Input.Buffer.Consume(BufferedAction.Kiai)) return;
            if (spiritKiai.TryGetValue(p, out int k) && k > 0) return;
            spiritKiai[p] = SpiritKiaiCooldown;
            Color pale = new Color(0.75f, 0.85f, 1f, 0.9f);
            Fx.Emit(FxType.Kiai, p.Position, Vector2.up, SpiritKiaiRange, pale);
            Fx.Popup(Strings.PopupSpiritKiai, p.Position + Vector2.up * 1.2f, pale);
            foreach (Enemy e in Enemies)
            {
                if (!e.CanBeHit || e.IsAlly || Vector2.Distance(e.Position, p.Position) > SpiritKiaiRange + e.Radius) continue;
                AttackDef a = e.CurrentAttack;
                if (a != null && e.FramesUntilActive > -2 && e.FramesUntilActive <= 18) e.Interrupt(15f, 30);
                else e.ApplyPosture(15f);
            }
            RegisterKiai(p);
        }

        /// <summary>Seconds until a fallen duelist's next spirit war cry (0 = ready).</summary>
        public float SpiritKiaiWait(PlayerController p) => spiritKiai.TryGetValue(p, out int k) ? k / (float)SimClock.TickRate : 0f;
    }
}
