using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>The four peoples of the Shuradō (design doc §5.3).</summary>
    public enum EnemyFaction
    {
        Human,
        Yokai,
        Oni,
        Dead,
    }

    /// <summary>
    /// Who belongs to whom. Rival factions turn on each other when the other's blow lands on
    /// them (a reversal, a shove, a turned arrow, a bomb); the healer mends only her own; the
    /// Onryō raises fallen humans; the Kappa runs from fire; the paper lantern draws
    /// will-o'-wisps that burst on the Oni.
    /// </summary>
    public static class Factions
    {
        static readonly HashSet<string> Yokai = new HashSet<string>
        {
            "tengu", "lanterna", "kappa", "lodo", "lodo_pequeno", "olho", "boxeador", "raijin", "raijin_sombra", "kitsune",
            "kamaitachi", "rokurokubi", "aranha_menor", "nopperabo", "ittan_momen", "omukade", "tsukumogami", "jorogumo", "yuki_onna",
        };

        static readonly HashSet<string> Oni = new HashSet<string>
        {
            "ciclope", "berserker", "nurikabe", "nue", "ashura", "braco_ashura", "kagutsuchi",
        };

        static readonly HashSet<string> Dead = new HashSet<string>
        {
            "onryo", "esqueleto", "vampiro", "hitodama", "gashadokuro_colossal", "mao_colossal", "o_kyuketsuki",
        };

        public static EnemyFaction Of(string id)
        {
            if (id == null) return EnemyFaction.Human;
            if (Yokai.Contains(id)) return EnemyFaction.Yokai;
            if (Oni.Contains(id)) return EnemyFaction.Oni;
            if (Dead.Contains(id)) return EnemyFaction.Dead;
            return EnemyFaction.Human;
        }

        public static EnemyFaction Of(EnemyDef d) => Of(d?.id);

        /// <summary>Two enemies of different peoples: a blow from one turns the other on it.</summary>
        public static bool Hostile(EnemyDef a, EnemyDef b) => a != null && b != null && Of(a) != Of(b);

        public static string Name(EnemyFaction f)
        {
            switch (f)
            {
                case EnemyFaction.Yokai: return "Yokai";
                case EnemyFaction.Oni: return "Oni";
                case EnemyFaction.Dead: return "Mortos";
                default: return "Humanos";
            }
        }

        public static Color Color(EnemyFaction f)
        {
            switch (f)
            {
                case EnemyFaction.Yokai: return ColorUtil.Hex("#7FD39B");
                case EnemyFaction.Oni: return ColorUtil.Hex("#E76F51");
                case EnemyFaction.Dead: return ColorUtil.Hex("#B07CFF");
                default: return ColorUtil.Hex("#E9C46A");
            }
        }

        static readonly Dictionary<string, EnemyDef> Possessed = new Dictionary<string, EnemyDef>();

        /// <summary>A fallen human raised by an Onryō: weaker, purple, and gone when the Onryō falls.</summary>
        public static EnemyDef PossessedOf(EnemyDef human)
        {
            if (Possessed.TryGetValue(human.id, out EnemyDef d)) return d;
            d = human.Clone();
            d.displayName = "Possuído";
            d.tint = ColorUtil.Hex("#B98CFF");
            d.maxHealth = human.maxHealth * 0.5f;
            d.minion = true;
            d.techniqueId = null;
            d.lore = "Um corpo humano levantado por um Onryō.";
            Possessed[human.id] = d;
            return d;
        }
    }

    /// <summary>A group that fights together (design doc §5.4).</summary>
    public sealed class FormationDef
    {
        public string id;
        public string name;
        public string[] members;
        public string hint;
    }

    public static class Formations
    {
        public const string Phalanx = "falange";
        public const string Ritual = "ritual";
        public const string Hunt = "cacada";
        public const string Bridge = "ponte";
        public const string Storm = "tempestade";
        /// <summary>One wave in this many (in fights deep enough) is a formation.</summary>
        public const float Chance = 0.18f;

        public static readonly FormationDef[] All =
        {
            new FormationDef { id = Phalanx, name = "Falange", members = new[] { "escudeiro", "lanceiro", "lanceiro" },
                hint = "Os lanceiros atacam por trás do escudo e mal sentem os golpes: quebre o escudeiro primeiro." },
            new FormationDef { id = Ritual, name = "Ritual", members = new[] { "onmyoji", "curandeira" },
                hint = "Um invoca, a outra cura. Interrompa um dos dois (um parry, uma quebra) e os dois caem juntos." },
            new FormationDef { id = Hunt, name = "Caçada", members = new[] { "kunoichi", "sombra" },
                hint = "Quando a kunoichi ataca, a Sombra aparece nas suas costas." },
            new FormationDef { id = Bridge, name = "Ponte", members = new[] { "arqueiro", "carrasco" },
                hint = "O Carrasco segura você para o Arqueiro acertar: não deixe a corrente fechar." },
            new FormationDef { id = Storm, name = "Tempestade", members = new[] { "raijin", "olho" },
                hint = "As cópias do Ninja do Trovão desviam os raios do Olho Vigia na sua direção." },
        };

        public static FormationDef Get(string id)
        {
            foreach (FormationDef f in All)
            {
                if (f.id == id) return f;
            }
            return null;
        }

        /// <summary>A formation wave for this depth, or null (the room fills the wave as usual).</summary>
        public static EnemySpawn[] TryPick(ulong seed, int depth)
        {
            var rng = new SeededRandom(seed);
            if (rng.NextFloat() >= Chance) return null;
            var open = new List<FormationDef>();
            foreach (FormationDef f in All)
            {
                bool ok = true;
                foreach (string m in f.members) ok &= RoomGenerator.UnlockDepth(m) <= depth;
                if (ok) open.Add(f);
            }
            if (open.Count == 0) return null;
            FormationDef pick = open[rng.Range(0, open.Count)];
            var wave = new EnemySpawn[pick.members.Length];
            for (int i = 0; i < wave.Length; i++) wave[i] = new EnemySpawn(pick.members[i]);
            return wave;
        }

        /// <summary>Which formation a wave forms (all its members present), if any.</summary>
        public static FormationDef Detect(List<Enemy> wave)
        {
            foreach (FormationDef f in All)
            {
                var need = new List<string>(f.members);
                foreach (Enemy e in wave) need.Remove(e.Def.id);
                if (need.Count == 0) return f;
            }
            return null;
        }
    }
}
