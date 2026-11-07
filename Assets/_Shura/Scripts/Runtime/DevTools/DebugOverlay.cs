using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shura
{
    /// <summary>
    /// F1 toggles an overlay with FPS, frame data and hurtboxes. F2-F5 are development
    /// cheats. Only active in the editor and development builds (Debug.isDebugBuild).
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        Game game;
        bool visible;
        float fps;
        GUIStyle style;
        readonly StringBuilder sb = new StringBuilder(1024);

        public void Init(Game owner)
        {
            game = owner;
        }

        void Update()
        {
            if (game == null) return;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime), 0.05f);
            if (!Debug.isDebugBuild) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame)
            {
                visible = !visible;
                game.ShowHitboxes = visible;
            }
            if (game.CurrentMode != Game.Mode.Playing) return;
            if (kb.f2Key.wasPressedThisFrame)
            {
                game.World.PlayerInvincible = !game.World.PlayerInvincible;
                game.UI.Toast(string.Format(Strings.ToastInvincible, game.World.PlayerInvincible ? Strings.On : Strings.Off), Color.white);
            }
            if (kb.f3Key.wasPressedThisFrame) game.World.DevKillAllEnemies();
            if (kb.f4Key.wasPressedThisFrame) game.World.DevSkipRoom();
            if (kb.f5Key.wasPressedThisFrame) game.World.AddFlow(5f);
        }

        void OnGUI()
        {
            if (!visible || game == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };
            GUI.matrix = Matrix4x4.identity;

            sb.Clear();
            sb.Append("FPS ").Append(fps.ToString("0")).Append("   tick ").Append(game.Clock.TickCount)
              .Append("   hitstop ").Append(game.Clock.HitstopFrames)
              .Append("   ritmo ").Append(game.EffectiveTempo.ToString("0.00")).Append('\n');
            sb.Append("modo ").Append(game.CurrentMode);
            RunState run = game.Run;
            if (run != null)
            {
                sb.Append("   semente ").Append(run.Seed).Append('\n');
                PlayerController p = game.World.Player;
                sb.Append("jogador ").Append(p.CurrentState).Append(" f").Append(p.StateFrame)
                  .Append("  combo ").Append(p.ComboIndex)
                  .Append("  janelaParry ").Append(p.ParryWindowFrames)
                  .Append("  iframes ").Append(p.IsDodgeInvulnerable ? "SIM" : "não").Append('\n');
                sb.Append("vida ").Append(p.Health.ToString("0")).Append('/').Append(p.MaxHealth.ToString("0"))
                  .Append("  postura ").Append(p.Posture.Value.ToString("0")).Append('/').Append(p.Posture.Max.ToString("0"))
                  .Append("  esquiva ").Append(p.DodgeCharges.ToString("0.00")).Append('\n');
                sb.Append("fluxo ").Append(run.Flow.Level).Append(" (").Append(run.Flow.Points.ToString("0.0")).Append(" pts)")
                  .Append("  invencível ").Append(game.World.PlayerInvincible ? "SIM" : "não").Append('\n');
                object holder = game.World.Coordinator.Holder;
                sb.Append("token de ataque: ").Append(holder is Enemy he ? he.Def.displayName : "livre").Append('\n');
                foreach (Enemy e in game.World.Enemies)
                {
                    sb.Append("· ").Append(e.Def.displayName).Append(' ').Append(e.CurrentState).Append(" f").Append(e.StateFrame);
                    if (e.CurrentMove != null)
                    {
                        sb.Append("  ").Append(e.CurrentMove.id).Append('[').Append(e.StepIndex).Append("] f").Append(e.StepFrame);
                    }
                    sb.Append("  hp ").Append(e.Health.ToString("0")).Append("  post ").Append(e.Posture.Value.ToString("0"));
                    if (e.Mastered) sb.Append("  SELO");
                    sb.Append('\n');
                }
            }
            else
            {
                sb.Append('\n');
            }
            sb.Append("\nF1 overlay  F2 invencível  F3 matar todos  F4 pular sala  F5 +Fluxo");

            var rect = new Rect(8f, 190f, 560f, 340f);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 12f), sb.ToString(), style);
        }
    }
}
