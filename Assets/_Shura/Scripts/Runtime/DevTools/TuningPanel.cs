#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shura
{
    /// <summary>
    /// Painel de afinação (WP-02), F1 em build de desenvolvimento: edita ao vivo os números do
    /// Balance.json (jogador, inimigos, Fluxo, sensação, escala), salva e recarrega o arquivo,
    /// liga e desliga as feature flags, mostra hitboxes, pula a sala e cria inimigos. Não existe
    /// em build final (o arquivo inteiro fica fora da compilação).
    /// </summary>
    public sealed class TuningPanel : MonoBehaviour
    {
        Game game;
        bool visible;
        int tab;
        int section;
        int enemyIndex;
        Vector2 scroll;
        readonly Dictionary<string, string> edits = new Dictionary<string, string>();
        GUIStyle label, small;
        static readonly string[] Tabs = { "Afinação", "Flags", "Ferramentas" };
        static readonly string[] Sections = { "player", "enemies", "flow", "feel", "scaling" };

        public void Init(Game owner)
        {
            game = owner;
            // Capturas de tela automáticas: "-shuraPanel" abre o painel desde o início.
            visible = Array.IndexOf(Environment.GetCommandLineArgs(), "-shuraPanel") >= 0;
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) visible = !visible;
            if (!visible) return;
            // O painel usa o mouse: cursor livre enquanto ele está aberto.
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        object SectionObject(int i)
        {
            switch (Sections[i])
            {
                case "player": return game.Config.player;
                case "enemies": return game.Config.enemies;
                case "flow": return game.Config.flow;
                case "feel": return game.Config.feel;
                default: return Balance.Scaling;
            }
        }

        void OnGUI()
        {
            if (!visible || game == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            }
            GUI.matrix = Matrix4x4.identity;
            // Na frente do HUD do jogo (menor profundidade = desenhado por cima).
            GUI.depth = -1000;
            var area = new Rect(Screen.width - 470f, 10f, 460f, Mathf.Min(Screen.height - 20f, 760f));
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 8f, area.y + 6f, area.width - 16f, area.height - 12f));
            GUILayout.Label("Painel de afinação (F1) · Balance: " + Balance.Source + " · Flags: " + Feature.Source, small);
            tab = GUILayout.Toolbar(tab, Tabs);
            switch (tab)
            {
                case 0: DrawTuning(); break;
                case 1: DrawFlags(); break;
                default: DrawTools(); break;
            }
            GUILayout.EndArea();
        }

        void DrawTuning()
        {
            section = GUILayout.Toolbar(section, Sections);
            object target = SectionObject(section);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(560f));
            foreach (FieldInfo f in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType != typeof(int) && f.FieldType != typeof(float)) continue;
                string key = Sections[section] + "." + f.Name;
                object v = f.GetValue(target);
                GUILayout.BeginHorizontal();
                GUILayout.Label(f.Name, label, GUILayout.Width(210f));
                if (GUILayout.Button("−", GUILayout.Width(24f))) Step(f, target, -1);
                string shown = edits.TryGetValue(key, out string e) ? e : Format(v);
                string typed = GUILayout.TextField(shown, GUILayout.Width(110f));
                if (typed != shown)
                {
                    edits[key] = typed;
                    if (TryParse(f.FieldType, typed, out object parsed)) f.SetValue(target, parsed);
                }
                if (GUILayout.Button("+", GUILayout.Width(24f))) Step(f, target, 1);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Salvar Balance.json"))
            {
                Balance.Save(game.Config);
                game.UI.Toast("Balance.json salvo", Color.white);
            }
            if (GUILayout.Button("Recarregar do arquivo"))
            {
                Balance.Load(game.Config);
                edits.Clear();
                game.UI.Toast("Balance.json recarregado", Color.white);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Os valores valem no próximo golpe. Quadros = 1/60 s; frações (0.12 = 12%).", small);
        }

        void Step(FieldInfo f, object target, int dir)
        {
            if (f.FieldType == typeof(int)) f.SetValue(target, (int)f.GetValue(target) + dir);
            else
            {
                float v = (float)f.GetValue(target);
                float step = Mathf.Abs(v) >= 1f ? 0.5f : Mathf.Abs(v) >= 0.1f ? 0.05f : 0.01f;
                f.SetValue(target, (float)Math.Round(v + dir * step, 4));
            }
            edits.Clear();
        }

        static string Format(object v) => v is float fl ? fl.ToString("0.####", CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture);

        static bool TryParse(Type t, string s, out object value)
        {
            value = null;
            s = (s ?? "").Replace(',', '.');
            if (t == typeof(int) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            {
                value = i;
                return true;
            }
            if (t == typeof(float) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                value = f;
                return true;
            }
            return false;
        }

        void DrawFlags()
        {
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(600f));
            foreach (string n in Feature.Names)
            {
                bool v = Feature.Get(n);
                bool nv = GUILayout.Toggle(v, " " + n + (v == Feature.DefaultOf(n) ? "" : "  (mudada)"));
                if (nv != v) Feature.Set(n, nv);
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Salvar features.json")) Feature.Save();
            if (GUILayout.Button("Padrão novo")) Feature.Reset();
            if (GUILayout.Button("Tudo v0.15")) Feature.SetAll(true);
            GUILayout.EndHorizontal();
            GUILayout.Label("Algumas flags só valem na próxima sala ou na próxima run.", small);
        }

        void DrawTools()
        {
            game.ShowHitboxes = GUILayout.Toggle(game.ShowHitboxes, " Mostrar hitboxes");
            bool playing = game.CurrentMode == Game.Mode.Playing && game.Run != null;
            GUI.enabled = playing;
            if (GUILayout.Button("Pular sala")) game.World.DevSkipRoom();
            if (GUILayout.Button("Matar todos os inimigos")) game.World.DevKillAllEnemies();
            if (GUILayout.Button("Invencível: " + (game.World.PlayerInvincible ? "sim" : "não"))) game.World.PlayerInvincible = !game.World.PlayerInvincible;
            GUILayout.Space(8f);
            EnemyDef[] all = EnemyDefs.All;
            enemyIndex = Mathf.Clamp(enemyIndex, 0, all.Length - 1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30f))) enemyIndex = (enemyIndex + all.Length - 1) % all.Length;
            GUILayout.Label(all[enemyIndex].displayName + " (" + all[enemyIndex].id + ")", label, GUILayout.Width(300f));
            if (GUILayout.Button(">", GUILayout.Width(30f))) enemyIndex = (enemyIndex + 1) % all.Length;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Criar este inimigo perto de mim")) game.World.DevSpawnEnemy(all[enemyIndex]);
            GUI.enabled = true;
            if (!playing) GUILayout.Label("As ferramentas de sala funcionam durante uma run.", small);
        }
    }
}
#endif
