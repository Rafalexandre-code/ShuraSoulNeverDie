using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>How a player strike moves the weapon (and therefore how it reads on screen).</summary>
    public enum SwingStyle
    {
        /// <summary>Arc from one side to the other.</summary>
        Sweep,
        /// <summary>Straight stab: the weapon extends forward.</summary>
        Thrust,
        /// <summary>Overhead blow that comes down in front.</summary>
        Slam,
        /// <summary>Full turn around the body.</summary>
        Spin,
        /// <summary>Short punch or stab, alternating hands.</summary>
        Jab,
    }

    /// <summary>Each weapon kills in its own way.</summary>
    public enum FinisherStyle
    {
        /// <summary>Dash through; a beat of silence; the enemy splits in two.</summary>
        CleanCut,
        /// <summary>Raise the blade high and bring it down: crushing blow, blood pool.</summary>
        Execution,
        /// <summary>A storm of stabs from every side, then collapse.</summary>
        ThousandCuts,
        /// <summary>Run it through, lift it on the polearm, throw it away.</summary>
        Impale,
        /// <summary>Chain around the neck: swing the enemy around you and release.</summary>
        Reap,
        /// <summary>Three body blows, then a fist straight through.</summary>
        HeartBreak,
    }

    /// <summary>What a weapon's swap strike or finisher does on top of the blow (the new arsenal, design doc §4.2).</summary>
    public enum WeaponPower
    {
        None,
        /// <summary>Tessen: every enemy projectile on screen flies back; allies' posture eases.</summary>
        FoldedWind,
        /// <summary>Kanabō: the ground shakes and everyone near is stunned.</summary>
        Earthquake,
        /// <summary>Shakujō: the bell makes every spirit solid and stuns the yokai near.</summary>
        Toll,
        /// <summary>Wakizashi: a step through the shadows to the back of the nearest enemy.</summary>
        DoubleShadow,
        /// <summary>Tessen finisher: the fan snaps shut and a gust cuts the line ahead.</summary>
        CloseFan,
        /// <summary>Kanabō finisher: the blow cracks the floor around the body.</summary>
        CrushOni,
        /// <summary>Shakujō finisher: yokai near are banished (minions vanish, the rest are scorched).</summary>
        Exorcism,
        /// <summary>Wakizashi finisher: you vanish; those near lose you for a while.</summary>
        HiddenTruth,
    }

    /// <summary>One player strike: a combo step or a weapon's swap strike.</summary>
    public sealed class PlayerAttackDef
    {
        public string name;
        public SwingStyle style;
        public int windup;
        public int active;
        public int recovery;
        public float damage;
        public float posture;
        public HitShape shape;
        public float lunge;
        /// <summary>Weapon angle (relative to facing) at the start and end of a Sweep.</summary>
        public float fromAngle;
        public float toAngle;
        /// <summary>How far the weapon reaches out (Thrust/Jab/chain).</summary>
        public float extend;
        /// <summary>Times the strike can hit the same enemy during its active frames.</summary>
        public int hits = 1;
        /// <summary>Drags hit enemies toward the player (chain weapons).</summary>
        public float pull;
        /// <summary>Distance the player travels during the active frames (swap strikes).</summary>
        public float dash;

        public int TotalFrames => windup + active + recovery;
    }

    public sealed class WeaponDef
    {
        public string id;
        public string name;
        public string description;
        public Shape sprite;
        /// <summary>Weapon sprite in Resources/Art/NA; the Shape is the fallback.</summary>
        public string art;
        public Color color;
        public float length;
        /// <summary>Second weapon drawn in the off hand (daggers, gauntlets).</summary>
        public bool dual;
        /// <summary>Draw a chain from the hand to the weapon when it is thrown out.</summary>
        public bool chain;
        public float moveMult = 1f;
        public Color trail = Color.white;
        public PlayerAttackDef[] combo;
        public PlayerAttackDef swapStrike;
        public FinisherStyle finisher;
        public string finisherName;
        /// <summary>Pitch of this weapon's swing sound: heavy weapons whoosh low.</summary>
        public float swingPitch = 1f;
        /// <summary>Degrees of the flourish after the last combo hit (the katana's chiburi blade flick).</summary>
        public float flourish;

        // ── The new arsenal's traits (design doc §4.2) ───────────────────────────────
        /// <summary>Every hit stuns for this many frames (Kanabō).</summary>
        public int stunOnHit;
        /// <summary>Holding parry does not block with this in hand (the perfect window still parries).</summary>
        public bool noBlock;
        /// <summary>Strikes make ethereal enemies solid (Shakujō).</summary>
        public bool exposes;
        /// <summary>Damage multiplier on yokai.</summary>
        public float spiritMult = 1f;
        /// <summary>Damage multiplier on a blow from behind.</summary>
        public float backstabMult = 1f;
        /// <summary>Swings knock enemy projectiles back (Tessen).</summary>
        public bool deflects;
        /// <summary>The combo step that also throws a kunai that comes back (-1 = none).</summary>
        public int kunaiStep = -1;
        public WeaponPower swapPower;
        public WeaponPower finisherPower;

        // ── The modular forge (design doc §4.1) ──────────────────────────────────────
        /// <summary>The blade's own id (mastery and the Ougi follow the blade); null = this id.</summary>
        public string baseId;
        public string gripId;
        public string tsubaId;
        public string BaseId => baseId ?? id;

        public WeaponDef Clone() => (WeaponDef)MemberwiseClone();
    }

    /// <summary>
    /// The arsenal. Frame data at 60 Hz. Every weapon trades reach, speed, damage and posture
    /// differently, and carries a Swap Strike (used after a Troca Perfeita) and a finisher.
    /// </summary>
    public static class WeaponDefs
    {
        static PlayerAttackDef Sweep(int w, int a, int r, float dmg, float post, float range, float arc, float lunge, float from, float to)
        {
            return new PlayerAttackDef
            {
                style = SwingStyle.Sweep, windup = w, active = a, recovery = r, damage = dmg, posture = post,
                shape = HitShape.Arc(range, arc), lunge = lunge, fromAngle = from, toAngle = to,
            };
        }

        static PlayerAttackDef Thrust(int w, int a, int r, float dmg, float post, float length, float width, float lunge, float extend)
        {
            return new PlayerAttackDef
            {
                style = SwingStyle.Thrust, windup = w, active = a, recovery = r, damage = dmg, posture = post,
                shape = HitShape.Line(length, width), lunge = lunge, extend = extend,
            };
        }

        static PlayerAttackDef Jab(int w, int a, int r, float dmg, float post, float length, float width, float lunge, float extend)
        {
            PlayerAttackDef d = Thrust(w, a, r, dmg, post, length, width, lunge, extend);
            d.style = SwingStyle.Jab;
            return d;
        }

        static PlayerAttackDef Spin(int w, int a, int r, float dmg, float post, float radius, int hits)
        {
            return new PlayerAttackDef
            {
                style = SwingStyle.Spin, windup = w, active = a, recovery = r, damage = dmg, posture = post,
                shape = HitShape.Circle(radius), hits = hits, fromAngle = 90f, toAngle = 450f,
            };
        }

        static PlayerAttackDef Slam(int w, int a, int r, float dmg, float post, HitShape shape, float lunge)
        {
            return new PlayerAttackDef
            {
                style = SwingStyle.Slam, windup = w, active = a, recovery = r, damage = dmg, posture = post,
                shape = shape, lunge = lunge, fromAngle = 150f, toAngle = 0f, extend = 0.2f,
            };
        }

        static PlayerAttackDef Named(PlayerAttackDef d, string name, float dash = 0f, int hits = 0, float pull = 0f, float extend = -1f)
        {
            d.name = name;
            d.dash = dash;
            if (hits > 0) d.hits = hits;
            if (pull > 0f) d.pull = pull;
            if (extend >= 0f) d.extend = extend;
            return d;
        }

        public static readonly WeaponDef Katana = new WeaponDef
        {
            id = "katana", art = "weapon_Katana", name = "Katana",
            description = "Equilibrada. Três cortes rápidos que alternam de lado.",
            sprite = Shape.Blade, color = ColorUtil.Hex("#E9EEF5"), length = 1.15f, trail = Color.white,
            combo = new[]
            {
                Sweep(5, 4, 13, 10f, 7f, 1.45f, 110f, 0.35f, 72f, -72f),
                Sweep(5, 4, 13, 10f, 7f, 1.45f, 110f, 0.35f, -72f, 72f),
                Sweep(6, 5, 21, 13f, 10f, 1.55f, 140f, 0.5f, 100f, -100f),
            },
            swapStrike = Named(Thrust(4, 6, 16, 24f, 35f, 2.8f, 0.9f, 0f, 0.5f), "Iaijutsu", dash: 2.6f),
            finisher = FinisherStyle.CleanCut, finisherName = "Corte Limpo", flourish = 70f,
        };

        public static readonly WeaponDef Montante = new WeaponDef
        {
            id = "montante", art = "weapon_BigSword", name = "Montante",
            description = "Espadão lento e devastador. Cortes largos que esmagam a postura.",
            sprite = Shape.Greatsword, color = ColorUtil.Hex("#C9CDD6"), length = 1.65f, moveMult = 0.85f,
            trail = ColorUtil.Hex("#FFD9A0"), swingPitch = 0.7f,
            combo = new[]
            {
                Sweep(12, 6, 20, 22f, 15f, 2.0f, 160f, 0.5f, 120f, -120f),
                Slam(14, 5, 28, 30f, 22f, HitShape.Arc(2.1f, 70f), 0.8f),
            },
            swapStrike = Named(Slam(10, 4, 20, 26f, 40f, HitShape.Circle(2.3f), 0f), "Queda do Céu", dash: 1.5f),
            finisher = FinisherStyle.Execution, finisherName = "Execução",
        };

        public static readonly WeaponDef Gemeas = new WeaponDef
        {
            id = "gemeas", art = "weapon_Sai", name = "Lâminas Gêmeas",
            description = "Duas adagas. Cinco golpes velozes; dano baixo por golpe, ritmo altíssimo.",
            sprite = Shape.Blade, color = ColorUtil.Hex("#F0E0E6"), length = 0.72f, dual = true, moveMult = 1.12f,
            trail = ColorUtil.Hex("#FFC2D1"), swingPitch = 1.35f,
            combo = new[]
            {
                Sweep(3, 2, 6, 5f, 4f, 1.15f, 90f, 0.2f, 50f, -50f),
                Sweep(3, 2, 6, 5f, 4f, 1.15f, 90f, 0.2f, -50f, 50f),
                Jab(3, 2, 6, 5f, 4f, 1.2f, 0.6f, 0.25f, 0.35f),
                Sweep(3, 2, 6, 5f, 4f, 1.15f, 90f, 0.2f, 50f, -50f),
                Spin(4, 6, 16, 8f, 6f, 1.3f, 2),
            },
            swapStrike = Named(Spin(3, 18, 12, 6f, 6f, 1.6f, 6), "Dança das Lâminas"),
            finisher = FinisherStyle.ThousandCuts, finisherName = "Mil Cortes", flourish = 35f,
        };

        public static readonly WeaponDef Naginata = new WeaponDef
        {
            id = "naginata", art = "weapon_Lance2", name = "Naginata",
            description = "Lâmina em haste longa. Estocadas de longe e um varrido enorme.",
            sprite = Shape.Glaive, color = ColorUtil.Hex("#D8D2C4"), length = 2.0f, moveMult = 0.95f,
            trail = ColorUtil.Hex("#CFE8FF"), swingPitch = 0.85f,
            combo = new[]
            {
                Thrust(7, 4, 14, 11f, 8f, 2.5f, 0.6f, 0.4f, 0.6f),
                Thrust(6, 4, 14, 11f, 8f, 2.5f, 0.6f, 0.4f, 0.6f),
                Sweep(9, 6, 22, 14f, 11f, 2.3f, 200f, 0.3f, 130f, -130f),
            },
            swapStrike = Named(Thrust(4, 6, 18, 22f, 32f, 4.2f, 0.8f, 0f, 0.9f), "Estocada Relâmpago", dash: 3.0f),
            finisher = FinisherStyle.Impale, finisherName = "Empalar", flourish = 40f,
        };

        public static readonly WeaponDef Kusarigama = new WeaponDef
        {
            id = "kusarigama", art = "weapon_AxeTool", name = "Kusarigama",
            description = "Foice na corrente. Alcance enorme e golpes que puxam os inimigos para você.",
            sprite = Shape.Sickle, color = ColorUtil.Hex("#C8C2B8"), length = 0.8f, chain = true,
            trail = ColorUtil.Hex("#D6D0C4"), swingPitch = 1.15f,
            combo = new[]
            {
                Named(Sweep(8, 5, 14, 9f, 6f, 2.7f, 80f, 0.1f, 60f, -60f), null, pull: 1.2f, extend: 1.9f),
                Named(Sweep(8, 5, 14, 9f, 6f, 2.7f, 80f, 0.1f, -60f, 60f), null, pull: 1.2f, extend: 1.9f),
                Named(Spin(10, 8, 20, 11f, 9f, 2.6f, 2), null, pull: 1.5f, extend: 1.9f),
            },
            swapStrike = Named(Spin(6, 10, 16, 12f, 24f, 3.0f, 2), "Laço Ceifador", pull: 2.5f, extend: 2.2f),
            finisher = FinisherStyle.Reap, finisherName = "Ceifa",
        };

        public static readonly WeaponDef Manoplas = new WeaponDef
        {
            id = "manoplas", art = "weapon_Hammer", name = "Manoplas de Ferro",
            description = "Punhos blindados. Pouco dano na vida, mas destroem a postura em segundos.",
            sprite = Shape.Gauntlet, color = ColorUtil.Hex("#B8B0A6"), length = 0.55f, dual = true, moveMult = 1.08f,
            trail = ColorUtil.Hex("#FFB070"), swingPitch = 0.9f,
            combo = new[]
            {
                Jab(3, 3, 8, 5f, 11f, 1.15f, 0.7f, 0.25f, 0.45f),
                Jab(3, 3, 8, 5f, 11f, 1.15f, 0.7f, 0.25f, 0.45f),
                Jab(3, 3, 8, 5f, 11f, 1.15f, 0.7f, 0.25f, 0.45f),
                Jab(6, 4, 18, 8f, 18f, 1.3f, 0.8f, 0.4f, 0.55f),
            },
            swapStrike = Named(Jab(5, 4, 20, 14f, 50f, 1.6f, 1.0f, 0f, 0.6f), "Punho do Trovão", dash: 1.2f),
            finisher = FinisherStyle.HeartBreak, finisherName = "Coração Partido",
        };

        public static readonly WeaponDef Tessen = new WeaponDef
        {
            id = "tessen", art = "weapon_Fan", name = "Tessen",
            description = "Leque de guerra. Cada abano rebate flechas; a troca devolve todas e alivia a guarda do grupo.",
            sprite = Shape.Fan, color = ColorUtil.Hex("#E8D9B5"), length = 0.85f, moveMult = 1.06f,
            trail = ColorUtil.Hex("#C9F2D4"), swingPitch = 1.2f, deflects = true,
            combo = new[]
            {
                Sweep(4, 4, 10, 7f, 8f, 1.5f, 130f, 0.25f, 80f, -80f),
                Sweep(4, 4, 10, 7f, 8f, 1.5f, 130f, 0.25f, -80f, 80f),
                Spin(5, 6, 16, 9f, 10f, 1.7f, 1),
            },
            swapStrike = Named(Spin(4, 8, 14, 10f, 22f, 2.4f, 1), "Vento Dobrado"),
            swapPower = WeaponPower.FoldedWind,
            finisher = FinisherStyle.CleanCut, finisherName = "Fechar o Leque", finisherPower = WeaponPower.CloseFan, flourish = 50f,
        };

        public static readonly WeaponDef Kanabo = new WeaponDef
        {
            id = "kanabo", art = "weapon_Club", name = "Kanabō",
            description = "Clava de oni. Lentíssima e sem bloqueio, mas todo golpe atordoa.",
            sprite = Shape.Greatsword, color = ColorUtil.Hex("#8C6E54"), length = 1.45f, moveMult = 0.8f,
            trail = ColorUtil.Hex("#FF8A5C"), swingPitch = 0.55f, stunOnHit = 40, noBlock = true,
            combo = new[]
            {
                Slam(18, 6, 30, 34f, 30f, HitShape.Arc(2.0f, 90f), 0.6f),
                Sweep(20, 6, 32, 30f, 26f, 2.1f, 170f, 0.5f, 130f, -130f),
            },
            swapStrike = Named(Slam(12, 5, 24, 30f, 45f, HitShape.Circle(3.2f), 0f), "Terremoto"),
            swapPower = WeaponPower.Earthquake,
            finisher = FinisherStyle.Execution, finisherName = "Esmagar o Oni", finisherPower = WeaponPower.CrushOni,
        };

        public static readonly WeaponDef Shakujo = new WeaponDef
        {
            id = "shakujo", art = "weapon_Stick", name = "Shakujō",
            description = "Cajado de monge com anéis. Torna sólidos os etéreos e fere mais os yokai.",
            sprite = Shape.Staff, color = ColorUtil.Hex("#D9C27A"), length = 1.75f, moveMult = 0.97f,
            trail = ColorUtil.Hex("#FFE9A8"), swingPitch = 0.95f, exposes = true, spiritMult = 1.3f,
            combo = new[]
            {
                Thrust(6, 4, 14, 9f, 10f, 2.2f, 0.6f, 0.35f, 0.5f),
                Sweep(7, 5, 16, 10f, 10f, 2.1f, 180f, 0.3f, 110f, -110f),
                Spin(8, 8, 20, 11f, 12f, 2.0f, 2),
            },
            swapStrike = Named(Spin(5, 10, 16, 10f, 26f, 2.8f, 2), "Badalada"),
            swapPower = WeaponPower.Toll,
            finisher = FinisherStyle.Impale, finisherName = "Exorcismo", finisherPower = WeaponPower.Exorcism, flourish = 30f,
        };

        public static readonly WeaponDef Wakizashi = new WeaponDef
        {
            id = "wakizashi", art = "weapon_Ninjaku", name = "Wakizashi + Kunai",
            description = "Lâmina curta e kunai que voltam à mão. Golpes pelas costas causam o dobro.",
            sprite = Shape.Blade, color = ColorUtil.Hex("#DDE3EA"), length = 0.9f, moveMult = 1.1f,
            trail = ColorUtil.Hex("#9D8CFF"), swingPitch = 1.25f, backstabMult = 2f, kunaiStep = 2,
            combo = new[]
            {
                Sweep(3, 3, 8, 7f, 5f, 1.2f, 100f, 0.25f, 60f, -60f),
                Jab(3, 3, 8, 7f, 5f, 1.3f, 0.6f, 0.3f, 0.4f),
                Sweep(4, 4, 12, 9f, 6f, 1.3f, 120f, 0.3f, -70f, 70f),
            },
            swapStrike = Named(Sweep(2, 6, 14, 18f, 22f, 1.6f, 220f, 0f, 110f, -110f), "Sombra Dupla", hits: 2),
            swapPower = WeaponPower.DoubleShadow,
            finisher = FinisherStyle.CleanCut, finisherName = "Golpe da Verdade Oculta", finisherPower = WeaponPower.HiddenTruth, flourish = 60f,
        };

        public static readonly WeaponDef[] All = { Katana, Montante, Gemeas, Naginata, Kusarigama, Manoplas, Tessen, Kanabo, Shakujo, Wakizashi };

        static readonly Dictionary<string, WeaponDef> ById = BuildIndex();

        static Dictionary<string, WeaponDef> BuildIndex()
        {
            var index = new Dictionary<string, WeaponDef>();
            foreach (WeaponDef w in All) index[w.id] = w;
            return index;
        }

        public static WeaponDef Get(string id)
        {
            if (id != null && ById.TryGetValue(id, out WeaponDef w)) return w;
            return WeaponForge.TryCompose(id, out WeaponDef forged) ? forged : Katana;
        }

        /// <summary>A blade of the arsenal, or a forged weapon ("blade+grip+tsuba").</summary>
        public static bool Exists(string id) => id != null && (ById.ContainsKey(id) || WeaponForge.TryCompose(id, out _));

        /// <summary>Only the arsenal's own blades (no forged ids).</summary>
        public static bool IsBlade(string id) => id != null && ById.ContainsKey(id);
    }
}
