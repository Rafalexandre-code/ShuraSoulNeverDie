using UnityEngine;

namespace Shura
{
    /// <summary>
    /// O aviso de tempo na própria arma (pedido do diretor, no lugar do anel sobre o inimigo):
    /// enquanto o golpe se arma, a lâmina ganha um contorno de luz de 1 px na cor da resposta
    /// (azul aparar, vermelho esquivar, roxo Troca Perfeita, dourado finta, verde amaldiçoado),
    /// um brilho macio ao longo dela e faíscas que correm do cabo à ponta, cada vez mais rápidas.
    /// No instante de responder, um "kira" — estrela de luz — estoura na ponta e a lâmina
    /// clareia; depois o brilho cai até o impacto. Só visual.
    /// </summary>
    public sealed class WeaponGlow
    {
        static readonly Vector2[] Offsets = { new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f) };
        const int Sparks = 3;
        static Sprite starSprite;

        readonly SpriteRenderer blade;
        readonly SpriteRenderer[] outline = new SpriteRenderer[Offsets.Length];
        readonly SpriteRenderer bloom, star, starCore;
        readonly SpriteRenderer[] sparks = new SpriteRenderer[Sparks];
        readonly Color baseColor;
        float sparkClock;

        static readonly string[] Star =
        {
            "....w....",
            "....w....",
            "...wcw...",
            "..wcCcw..",
            "wwcCCCcww",
            "..wcCcw..",
            "...wcw...",
            "....w....",
            "....w....",
        };

        public WeaponGlow(SpriteRenderer weapon, Color weaponColor)
        {
            blade = weapon;
            baseColor = weaponColor;
            Transform t = weapon.transform;
            for (int i = 0; i < outline.Length; i++)
            {
                outline[i] = SpriteUtil.Make("Contorno", t, weapon.sprite, Color.white, weapon.sortingOrder - 1);
                outline[i].enabled = false;
            }
            bloom = SpriteUtil.Make("BrilhoDaLamina", t, ShapeLibrary.Get(Shape.SoftCircle), Color.white, weapon.sortingOrder - 2);
            bloom.enabled = false;
            if (starSprite == null)
            {
                var pal = new System.Collections.Generic.Dictionary<char, Color>
                {
                    { 'w', new Color(1f, 1f, 1f, 0.55f) }, { 'c', new Color(1f, 1f, 1f, 0.85f) }, { 'C', Color.white },
                };
                starSprite = PixelArt.FromGrid("kira_star", Star, pal, new Vector2(0.5f, 0.5f));
            }
            starCore = SpriteUtil.Make("KiraNucleo", t, starSprite, Color.white, Sorting.Overlay + 1);
            star = SpriteUtil.Make("Kira", t, ShapeLibrary.Get(Shape.SoftCircle), Color.white, Sorting.Overlay);
            starCore.enabled = star.enabled = false;
            for (int i = 0; i < Sparks; i++)
            {
                sparks[i] = SpriteUtil.Make("Faisca", t, ShapeLibrary.Get(Shape.Pixel), Color.white, weapon.sortingOrder + 1);
                sparks[i].enabled = false;
            }
        }

        public void Hide()
        {
            foreach (SpriteRenderer o in outline) o.enabled = false;
            bloom.enabled = star.enabled = starCore.enabled = false;
            foreach (SpriteRenderer s in sparks) s.enabled = false;
            blade.color = baseColor;
        }

        /// <summary>
        /// <paramref name="charge"/> 0..1 enquanto o golpe se arma; <paramref name="flash"/> 0..1 depois
        /// do instante de responder (1 = agora, cai até o impacto); -1 = ainda não chegou.
        /// </summary>
        public void Show(Color c, float charge, float flash, float dt, bool flicker)
        {
            Transform t = blade.transform;
            Vector3 ls = t.lossyScale;
            float sx = Mathf.Max(0.01f, Mathf.Abs(ls.x)), sy = Mathf.Max(0.01f, Mathf.Abs(ls.y));
            const float px = 1f / PixelArt.PixelsPerUnit;
            bool closed = flash >= 0f;
            float k = closed ? 1f : charge;
            // Finta: o brilho falha, como uma vela ao vento.
            float jitter = flicker ? (Mathf.Repeat(Time.time * 13f, 1f) < 0.35f ? 0.35f : 1f) : 1f;

            for (int i = 0; i < outline.Length; i++)
            {
                SpriteRenderer o = outline[i];
                o.enabled = true;
                o.sprite = blade.sprite;
                o.flipX = blade.flipX;
                o.flipY = blade.flipY;
                o.sortingOrder = blade.sortingOrder - 1;
                o.transform.localPosition = new Vector3(Offsets[i].x * px / sx, Offsets[i].y * px / sy, 0f);
                o.color = c.WithAlpha(Mathf.Lerp(0.25f, 1f, k) * jitter);
            }

            // O brilho macio: uma elipse esticada ao longo da lâmina (o sprite aponta em +X).
            bloom.enabled = true;
            bloom.sortingOrder = blade.sortingOrder - 2;
            bloom.transform.localPosition = new Vector3(0.55f, 0f, 0f);
            float bloomA = closed ? 0.55f * (0.4f + 0.6f * flash) : 0.08f + 0.3f * k * k;
            bloom.transform.localScale = new Vector3(1.5f, 0.55f / sy * sx * 0.5f + 0.35f, 1f);
            bloom.color = c.WithAlpha(bloomA * jitter);

            // A lâmina clareia no instante de responder.
            blade.color = closed ? Color.Lerp(baseColor, Color.white, 0.75f * flash) : Color.Lerp(baseColor, Color.Lerp(baseColor, c, 0.5f), k * 0.6f);

            // Faíscas do cabo à ponta, mais rápidas quanto mais perto do golpe.
            sparkClock += dt * Mathf.Lerp(0.9f, 3.2f, k);
            for (int i = 0; i < Sparks; i++)
            {
                SpriteRenderer s = sparks[i];
                float u = Mathf.Repeat(sparkClock + i / (float)Sparks, 1f);
                s.enabled = !closed || flash > 0.4f;
                s.sortingOrder = blade.sortingOrder + 1;
                float wobble = Mathf.Sin((sparkClock + i) * 17f) * 1.5f * px / sy;
                s.transform.localPosition = new Vector3(0.2f + u * 0.85f, wobble, 0f);
                s.transform.localScale = new Vector3(1.6f / sx, 1.6f / sy, 1f) * px * PixelArt.PixelsPerUnit / PixelArt.PixelsPerUnit;
                s.color = Color.Lerp(Color.white, c, u).WithAlpha(Mathf.Sin(u * Mathf.PI) * (0.5f + 0.5f * k) * jitter);
            }

            // O kira: a estrela de luz na ponta, no instante de responder.
            star.enabled = starCore.enabled = closed;
            if (!closed) return;
            float pop = 1f - flash;                     // 0 no instante, 1 no impacto
            float size = Mathf.Lerp(1.6f, 0.9f, pop);
            Vector3 tip = new Vector3(1f, 0f, 0f);
            star.transform.localPosition = tip;
            star.transform.localScale = new Vector3(size * 1.4f / sx, size * 1.4f / sy, 1f);
            star.color = c.WithAlpha(0.7f * flash);
            starCore.transform.localPosition = tip;
            starCore.transform.localRotation = Quaternion.Euler(0f, 0f, -t.eulerAngles.z);
            starCore.transform.localScale = new Vector3(size / sx, size / sy, 1f);
            starCore.color = Color.Lerp(Color.white, c, 0.25f).WithAlpha(Mathf.Clamp01(flash * 1.4f));
        }
    }
}
