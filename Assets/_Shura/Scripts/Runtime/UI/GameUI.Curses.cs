using UnityEngine;

namespace Shura
{
    /// <summary>
    /// As maldições que o duelista carrega viram ícones no cartão do HUD (selo escuro, ícone roxo
    /// que pulsa, salas que faltam no canto). Passar o mouse por cima abre uma caixa de texto que
    /// acompanha o cursor dizendo o que a maldição faz. Ícones: "1-bit Pixel Icons" de Nikoichu
    /// (CC0), em Resources/Art/Extra/curse_icons.png: sem cura, frágil, desajeitado, inquieto,
    /// Muramasa e o pentagrama genérico.
    /// </summary>
    public sealed partial class GameUI
    {
        const float CurseIconSize = 34f;
        GUIStyle tipStyle;
        static readonly Color CurseInk = ColorUtil.Hex("#C08CFF");
        static readonly Color CurseBlood = ColorUtil.Hex("#FF6F7D");
        static Sprite[] curseIcons;

        // A caixa da dica: preenchida no desenho do HUD, desenhada por cima de tudo no fim.
        string tipTitle, tipBody;
        Color tipColor;
        int tipFrame = -1;
        Vector2? tipAnchor;
        Sprite tipIcon;

        static Sprite CurseIcon(int index)
        {
            if (curseIcons == null)
            {
                curseIcons = new Sprite[6];
                var tex = Resources.Load<Texture2D>("Art/Extra/curse_icons");
                if (tex != null)
                {
                    tex.filterMode = FilterMode.Point;
                    for (int i = 0; i < curseIcons.Length; i++)
                        curseIcons[i] = Sprite.Create(tex, new Rect(i * 16, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
                }
            }
            return curseIcons[Mathf.Clamp(index, 0, curseIcons.Length - 1)];
        }

        /// <summary>
        /// Desenha os ícones das maldições a partir de (x, y) e devolve a largura usada; com o mouse
        /// em cima de um, prepara a dica.
        /// </summary>
        float DrawCurseIcons(float x, float y, HudData p)
        {
            float cx = x;
            RunCurse curse = p.curse != null && p.curseRooms > 0 ? RunCurses.FromName(p.curse) : RunCurse.None;
            if (curse != RunCurse.None)
            {
                string rooms = string.Format(Strings.CurseRoomsLeft, p.curseRooms);
                CurseIconBox(new Rect(cx, y, CurseIconSize, CurseIconSize), (int)curse - 1, CurseInk, p.curseRooms.ToString(),
                             RunCurses.Name(curse), RunCurses.Text(curse) + "\n" + rooms);
                cx += CurseIconSize + 6f;
            }
            if (p.muramasa)
            {
                CurseIconBox(new Rect(cx, y, CurseIconSize, CurseIconSize), 4, CurseBlood, null, Strings.CurseMuramasaTitle, Strings.CurseMuramasaText);
                cx += CurseIconSize + 6f;
            }
            return cx - x;
        }

        void CurseIconBox(Rect r, int icon, Color ink, string corner, string title, string body)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f + r.x * 0.1f);
            bool mouse = r.Contains(Event.current.mousePosition);
            // O tour de capturas não tem mouse: a dica abre sozinha, presa ao ícone.
            bool tour = game.Launch.ShotDir != null;
            bool hover = mouse || tour;
            kit.Dab(new Rect(r.x - 5f, r.y - 5f, r.width + 10f, r.height + 10f), ink.WithAlpha(hover ? 0.35f : 0.12f + 0.1f * pulse));
            kit.Dab(r, ColorUtil.Hex("#140A1E").WithAlpha(0.92f));
            Icon(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), CurseIcon(icon), Color.Lerp(ink, Color.white, hover ? 0.35f : 0.15f * pulse));
            if (corner != null)
                Label(new Rect(r.xMax - 4f, r.yMax - 16f, 20f, 18f), corner, capsStyle, Color.white);
            if (!hover) return;
            tipTitle = title;
            tipBody = body;
            tipColor = ink;
            tipIcon = CurseIcon(icon);
            tipFrame = Time.frameCount;
            tipAnchor = mouse ? (Vector2?)null : new Vector2(r.xMax, r.yMax);
        }

        /// <summary>A caixa da dica, colada ao cursor (e virando para dentro da tela perto das bordas).</summary>
        void DrawCurseTooltip()
        {
            if (tipFrame != Time.frameCount || string.IsNullOrEmpty(tipTitle)) return;
            Vector2 m = tipAnchor ?? Event.current.mousePosition;
            const float w = 380f;
            if (tipStyle == null) tipStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            float h = 50f + tipStyle.CalcHeight(new GUIContent(tipBody), w - 30f);
            Rect fs = FullScreen;
            float x = m.x + 22f, y = m.y + 18f;
            if (x + w > fs.xMax - 8f) x = m.x - w - 14f;
            if (y + h > fs.yMax - 8f) y = m.y - h - 10f;
            var box = new Rect(x, y, w, h);
            DrawRect(new Rect(box.x - 2f, box.y - 2f, box.width + 4f, box.height + 4f), tipColor.WithAlpha(0.55f));
            DrawRect(box, ColorUtil.Hex("#120A1A").WithAlpha(0.96f));
            DrawRect(new Rect(box.x, box.y, 4f, box.height), tipColor);
            Icon(new Rect(box.x + 14f, box.y + 8f, 26f, 26f), tipIcon, tipColor);
            Label(new Rect(box.x + 48f, box.y + 6f, w - 60f, 30f), tipTitle, capsStyle, tipColor);
            Label(new Rect(box.x + 16f, box.y + 42f, w - 30f, h - 46f), tipBody, tipStyle, UiKit.PaperText);
        }
    }
}
