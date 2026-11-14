using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Telemetria local (WP-01): um arquivo .jsonl por run em
    /// Application.persistentDataPath/telemetry/ (robôs: telemetry_bots/), uma linha por evento.
    /// Nada sai do computador; o jogador é avisado na primeira run e pode desligar nas opções.
    /// Todo evento leva t (quadros de simulação), runId, seed, depth, act e room.
    /// Lido por Tools/telemetry_report/telemetry_report.py.
    /// </summary>
    public sealed class TelemetryService
    {
        readonly Game game;
        StreamWriter writer;
        readonly StringBuilder sb = new StringBuilder(512);
        string runId;
        ulong seed;

        // Por sala.
        float roomStart;
        float roomDamage;
        int roomDrinksStart;
        readonly int[] roomPresses = new int[InputBuffer.Count];
        readonly Dictionary<string, int> roomFinishers = new Dictionary<string, int>();
        bool roomOpen;
        int rivalsAtStart;
        readonly List<string> offered = new List<string>();

        public string Folder { get; private set; }
        public string CurrentFile { get; private set; }
        public bool Recording => writer != null;

        public TelemetryService(Game owner)
        {
            game = owner;
            game.World.AttackResolved += OnAttackResolved;
            game.World.FinisherResolved += OnFinisher;
        }

        /// <summary>Desligada nas opções, ou "-shuraTelemetry off" na linha de comando: não grava nada.</summary>
        public bool Enabled
        {
            get
            {
                string arg = Arg();
                if (arg == "off") return false;
                return game.Settings.telemetry || arg != null;
            }
        }

        static string Arg()
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < a.Length; i++)
            {
                if (a[i] == "-shuraTelemetry") return a[i + 1];
            }
            return null;
        }

        string ResolveFolder()
        {
            string arg = Arg();
            if (!string.IsNullOrEmpty(arg) && arg != "off" && arg != "on") return arg;
            return Path.Combine(Application.persistentDataPath, game.Launch.Automated ? "telemetry_bots" : "telemetry");
        }

        // ── Run ──────────────────────────────────────────────────────────────────────

        public void BeginRun(RunState run)
        {
            EndRun("abandono");
            if (run == null || !Enabled) return;
            try
            {
                Folder = ResolveFolder();
                Directory.CreateDirectory(Folder);
                seed = run.Seed;
                runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + (seed % 100000UL);
                CurrentFile = Path.Combine(Folder, "run-" + runId + ".jsonl");
                writer = new StreamWriter(CurrentFile, false, new UTF8Encoding(false));
                rivalsAtStart = game.Meta.rivals.Count;
                Begin("run_start").Field("mode", run.Mode.ToString()).Field("weapons", Weapons()).Field("version", Application.version);
                Write();
                if (!game.Settings.telemetryNoticeShown && !game.Launch.Automated)
                {
                    game.Settings.telemetryNoticeShown = true;
                    game.Settings.Save();
                    game.UI.Toast(Strings.TelemetryNotice, game.Config.palette.uiText);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Shura][Telemetria] não foi possível abrir o arquivo: " + e.Message);
                writer = null;
            }
        }

        /// <summary>Fim da run: resultado ("morte", "vitoria", "abandono"), duração e o Algoz criado.</summary>
        public void EndRun(string result)
        {
            if (writer == null) return;
            CloseRoom();
            RunState run = game.Run;
            Begin("run_end").Field("result", result).Num("duration_s", run != null ? run.Elapsed : 0f)
                .Field("rooms", run != null ? run.RoomsCleared : 0).Field("algoz", game.Meta.rivals.Count > rivalsAtStart);
            Write();
            writer.Dispose();
            writer = null;
        }

        /// <summary>A morte: o que matou, onde, quanto tempo de run, e um resumo da build.</summary>
        public void Death()
        {
            if (writer == null) return;
            PlayerController h = game.World.Host;
            Begin("death").Field("source", h != null ? h.LastHurtBy : "").Num("run_time_s", game.Run.Elapsed).Field("build", BuildSummary());
            Write();
        }

        // ── Salas ────────────────────────────────────────────────────────────────────

        public void BeginRoom()
        {
            if (writer == null) return;
            CloseRoom();
            roomOpen = true;
            roomStart = game.Run.Elapsed;
            roomDamage = 0f;
            roomDrinksStart = game.Run.Profile.TotalDrinks;
            roomFinishers.Clear();
            int[] p = game.ActiveInput?.Buffer?.Presses;
            if (p != null) Array.Copy(p, roomPresses, p.Length);
        }

        /// <summary>Fim da sala: duração, dano recebido, curas, finalizações por tipo e nível, e as teclas usadas.</summary>
        public void CloseRoom()
        {
            if (writer == null || !roomOpen) return;
            roomOpen = false;
            RoomDef room = game.World.CurrentRoom;
            Begin("room_end").Field("kind", room != null ? room.kind.ToString() : "").Field("biome", room != null ? room.biome.name : "")
                .Num("duration_s", game.Run.Elapsed - roomStart).Num("damage_taken", roomDamage)
                .Field("heals", game.Run.Profile.TotalDrinks - roomDrinksStart);
            sb.Append(",\"finishers\":{");
            bool first = true;
            foreach (KeyValuePair<string, int> kv in roomFinishers)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Str(kv.Key).Append(':').Int(kv.Value);
            }
            sb.Append('}');
            Write();

            int[] p = game.ActiveInput?.Buffer?.Presses;
            if (p == null) return;
            Begin("input_use");
            sb.Append(",\"counts\":{");
            first = true;
            for (int i = 0; i < p.Length; i++)
            {
                int d = p[i] - roomPresses[i];
                if (d <= 0) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Str(((BufferedAction)i).ToString()).Append(':').Int(d);
            }
            sb.Append('}');
            Write();
        }

        // ── Recompensas e rota ───────────────────────────────────────────────────────

        public void RewardsOffered(IReadOnlyList<RewardDef> choices)
        {
            offered.Clear();
            foreach (RewardDef r in choices) offered.Add(r.Id);
            if (writer == null || choices.Count == 0) return;
            Begin("reward_offered");
            Ids("options", offered);
            Write();
        }

        public void RewardPicked(RewardDef picked)
        {
            if (writer == null || picked == null) return;
            Begin("reward_picked").Field("picked", picked.Id).Field("kind", picked.Kind.ToString());
            Ids("options", offered);
            Write();
        }

        public void RouteChosen(IReadOnlyList<RouteNode> options, RouteNode chosen)
        {
            if (writer == null) return;
            Begin("route_choice");
            var kinds = new List<string>();
            foreach (RouteNode n in options) kinds.Add(n.kind + "/" + n.hint);
            Ids("options", kinds);
            sb.Append(",\"chosen\":").Str(chosen != null ? chosen.kind + "/" + chosen.hint : "");
            Write();
        }

        // ── Combate ──────────────────────────────────────────────────────────────────

        /// <summary>O tipo de anel de um golpe, como o jogador o vê.</summary>
        public static string RingOf(AttackDef a)
        {
            if (a == null) return "?";
            if (a.groupStrike) return "grupo";
            if (a.feint) return "finta";
            if (a.cursed) return "maldito";
            if (a.kind == AttackKind.Spirit) return "roxo";
            return a.IsDangerous ? "vermelho" : "ciano";
        }

        void OnAttackResolved(PlayerController p, IncomingAttack inc, HitOutcome o, float damage, int sinceParry, int sinceSwap, int dodgeFrame)
        {
            if (writer == null || p == null || p != game.World.Host) return;
            int lead = game.Config.feel.timingCueLeadFrames;
            string response;
            int? delta = null;
            switch (o)
            {
                case HitOutcome.PerfectParry: response = "parry"; delta = lead - sinceParry; break;
                case HitOutcome.Blocked: response = "block"; delta = lead - sinceParry; break;
                case HitOutcome.SwapParry: response = "swap"; delta = lead - sinceSwap; break;
                case HitOutcome.PerfectDodge:
                case HitOutcome.CounterThrust: response = "dodge"; if (dodgeFrame >= 0) delta = lead - dodgeFrame; break;
                default:
                    // Levou o golpe: a resposta tentada (se houve) e o quanto errou.
                    if (sinceParry <= 30) { response = "parry"; delta = lead - sinceParry; }
                    else if (sinceSwap <= 30) { response = "swap"; delta = lead - sinceSwap; }
                    else if (dodgeFrame >= 0) { response = "dodge"; delta = lead - dodgeFrame; }
                    else response = "hit";
                    break;
            }
            bool success = o != HitOutcome.Hit;
            string ring = RingOf(inc.attack);
            string enemy = inc.attacker != null && inc.attacker.Def != null ? inc.attacker.Def.id : "";
            Begin("ring_resolved").Field("ring", ring).Field("enemy", enemy).Field("response", response).Field("outcome", o.ToString()).Field("success", success);
            if (delta.HasValue) sb.Append(",\"delta\":").Int(delta.Value);
            else sb.Append(",\"delta\":null");
            Write();

            if (damage <= 0f) return;
            roomDamage += damage;
            Begin("player_hit").Field("source", enemy + ":" + (inc.attack != null ? inc.attack.id : "")).Num("damage", damage).Field("ring", ring);
            bool? visible = Visible(inc.attacker != null ? inc.attacker.Position : inc.source);
            sb.Append(",\"ring_visible\":").Append(visible.HasValue ? (visible.Value ? "true" : "false") : "null");
            Write();
        }

        void OnFinisher(PlayerController p, Enemy e, string kind, string grade)
        {
            if (writer == null || p != game.World.Host) return;
            string key = kind + "_" + grade;
            roomFinishers.TryGetValue(key, out int n);
            roomFinishers[key] = n + 1;
        }

        /// <summary>O atacante estava na tela? (sem câmera, como nos robôs headless: desconhecido).</summary>
        bool? Visible(Vector2 world)
        {
            Camera cam = game.CameraRig != null ? game.CameraRig.Cam : null;
            if (cam == null || game.Launch.Headless) return null;
            Vector3 v = cam.WorldToViewportPoint(world);
            return v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        // ── Escrita ──────────────────────────────────────────────────────────────────

        TelemetryService Begin(string type)
        {
            sb.Clear();
            RunState run = game.Run;
            World w = game.World;
            int depth = w != null && w.CurrentRoom != null ? w.Depth : 0;
            sb.Append("{\"type\":").Str(type)
              .Append(",\"t\":").Int(run != null ? Mathf.RoundToInt(run.Elapsed * SimClock.TickRate) : 0)
              .Append(",\"runId\":").Str(runId ?? "")
              .Append(",\"seed\":").Append(seed.ToString(CultureInfo.InvariantCulture))
              .Append(",\"depth\":").Int(depth)
              // Até o WP-12 não há atos: o "ato" é o bioma de 5 salas (1, 2, 3...).
              .Append(",\"act\":").Int(depth / 5 + 1)
              .Append(",\"room\":").Int(run != null ? run.RoomIndex + 1 : 0);
            return this;
        }

        TelemetryService Field(string k, string v)
        {
            sb.Append(',').Str(k).Append(':').Str(v ?? "");
            return this;
        }

        TelemetryService Field(string k, int v)
        {
            sb.Append(',').Str(k).Append(':').Int(v);
            return this;
        }

        TelemetryService Field(string k, bool v)
        {
            sb.Append(',').Str(k).Append(':').Append(v ? "true" : "false");
            return this;
        }

        TelemetryService Num(string k, float v)
        {
            sb.Append(',').Str(k).Append(':').Num(v, 2);
            return this;
        }

        void Ids(string k, List<string> ids)
        {
            sb.Append(',').Str(k).Append(":[");
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Str(ids[i]);
            }
            sb.Append(']');
        }

        void Write()
        {
            if (writer == null) return;
            sb.Append('}');
            writer.WriteLine(sb.ToString());
            writer.Flush();
        }

        string Weapons()
        {
            RunState run = game.Run;
            if (run == null) return "";
            return (run.Weapons[0] != null ? run.Weapons[0].id : "") + "+" + (run.Weapons[1] != null ? run.Weapons[1].id : "");
        }

        string BuildSummary()
        {
            var parts = new List<string> { Weapons() };
            PlayerBuild b = game.Run?.Main;
            if (b != null)
            {
                foreach (RewardDef r in b.Owned) parts.Add(r.Id);
            }
            return string.Join(" ", parts);
        }
    }
}
