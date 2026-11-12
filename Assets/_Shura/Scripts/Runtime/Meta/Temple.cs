using UnityEngine;

namespace Shura
{
    /// <summary>A construction of the ruined temple and what it opens (design doc §7.1).</summary>
    public sealed class BuildingDef
    {
        public string id;
        public string name;
        public string kanji;
        /// <summary>What it opens, in one line.</summary>
        public string opens;
        public int ecos;
        public int steel;
        /// <summary>Its guests have not arrived yet: shown in ruins, cannot be rebuilt.</summary>
        public bool comingSoon;
        public string soonText;
    }

    /// <summary>
    /// The Templo em Ruínas, the hub between runs. Every construction is rebuilt with Ecos and
    /// Aço Celeste and opens one system: the Altar's tree, the Forge, the Dojo, the Library,
    /// the Zen Garden, the Lineage's graveyard (the Tea House and the Rivals' Wall come later).
    /// </summary>
    public static class Temple
    {
        public const string Altar = "altar";
        public const string Forge = "forja";
        public const string Dojo = "dojo";
        public const string Library = "biblioteca";
        public const string Garden = "jardim";
        public const string TeaHouse = "casa_cha";
        public const string RivalWall = "muralha";
        public const string Graveyard = "cemiterio";

        public static readonly BuildingDef[] All =
        {
            new BuildingDef { id = Altar, name = "Altar das Almas", kanji = "魂", opens = "A árvore de melhorias: Corpo, Lâmina e Mente." },
            new BuildingDef { id = Forge, name = "Forja", kanji = "鍛", ecos = 100, opens = "A forja modular: empunhaduras e tsubas para cada lâmina." },
            new BuildingDef { id = Dojo, name = "Dojo", kanji = "道", ecos = 150, steel = 2, opens = "Treino com o mestre, a maestria de cada arma e os Senseis." },
            new BuildingDef { id = Library, name = "Biblioteca", kanji = "書", ecos = 120, steel = 1, opens = "O bestiário de quem você enfrentou e a história do Shuradō." },
            new BuildingDef { id = Garden, name = "Jardim Zen", kanji = "庭", ecos = 200, steel = 3, opens = "Jurar Juramentos (Calor), escolher a semente e gastar pétalas." },
            new BuildingDef { id = TeaHouse, name = "Casa de Chá", kanji = "茶", ecos = 300, steel = 5, comingSoon = true,
                opens = "Hóspedes, conversas e missões pessoais.", soonText = "Os hóspedes ainda estão a caminho." },
            new BuildingDef { id = RivalWall, name = "Muralha dos Rivais", kanji = "壁", ecos = 300, steel = 5, comingSoon = true,
                opens = "Os rivais que lembram de você, com patente e cicatrizes.", soonText = "Ainda não há rivais para pendurar aqui." },
            new BuildingDef { id = Graveyard, name = "Cemitério da Linhagem", kanji = "墓", ecos = 80, opens = "Os túmulos dos discípulos, seus poemas e suas armas." },
        };

        public static BuildingDef Get(string id)
        {
            if (id == null) return null;
            foreach (BuildingDef b in All)
            {
                if (b.id == id) return b;
            }
            return null;
        }

        /// <summary>The Altar stands from the start; the rest must be rebuilt.</summary>
        public static bool IsBuilt(MetaProgress meta, BuildingDef b) => b.id == Altar || meta.Built(b.id);

        public static bool IsBuilt(MetaProgress meta, string id) => id == Altar || meta.Built(id);

        public static bool CanBuild(MetaProgress meta, BuildingDef b) =>
            !b.comingSoon && !IsBuilt(meta, b) && meta.ecos >= b.ecos && meta.steel >= b.steel;

        public static bool TryBuild(MetaProgress meta, BuildingDef b)
        {
            if (!CanBuild(meta, b)) return false;
            meta.ecos -= b.ecos;
            meta.steel -= b.steel;
            meta.buildings.Add(b.id);
            return true;
        }

        /// <summary>Aço Celeste a run brings home: what was left in the pouch and one per Guardian.</summary>
        public static int SteelForRun(int pouch, int guardians) => Mathf.Max(0, pouch) + Mathf.Max(0, guardians);
    }

    public sealed class BladeColorDef
    {
        public string id;
        public string name;
        public Color color;
        public int petals;
    }

    public sealed class OrnamentDef
    {
        public string id;
        public string name;
        public string kanji;
        public int petals;
    }

    /// <summary>What the petals buy: blade colours and the garden's ornaments.</summary>
    public static class Garden
    {
        public static readonly BladeColorDef[] Colors =
        {
            new BladeColorDef { id = "sakura", name = "Sakura", color = ColorUtil.Hex("#FF9EC4"), petals = 10 },
            new BladeColorDef { id = "lua", name = "Luar", color = ColorUtil.Hex("#BFE3FF"), petals = 12 },
            new BladeColorDef { id = "jade", name = "Jade", color = ColorUtil.Hex("#5FD3A0"), petals = 15 },
            new BladeColorDef { id = "brasa", name = "Brasa", color = ColorUtil.Hex("#FF7A3D"), petals = 15 },
            new BladeColorDef { id = "ouro", name = "Ouro Velho", color = ColorUtil.Hex("#F2C14E"), petals = 20 },
            new BladeColorDef { id = "sumi", name = "Sumi", color = ColorUtil.Hex("#3A3550"), petals = 25 },
        };

        public static readonly OrnamentDef[] Ornaments =
        {
            new OrnamentDef { id = "pedras", name = "Pedras do rio", kanji = "石", petals = 6 },
            new OrnamentDef { id = "bonsai", name = "Bonsai", kanji = "松", petals = 10 },
            new OrnamentDef { id = "lanterna", name = "Lanterna de pedra", kanji = "灯", petals = 12 },
            new OrnamentDef { id = "carpas", name = "Lago de carpas", kanji = "鯉", petals = 15 },
            new OrnamentDef { id = "ponte", name = "Ponte vermelha", kanji = "橋", petals = 18 },
            new OrnamentDef { id = "sino", name = "Sino do templo", kanji = "鐘", petals = 24 },
        };

        public static BladeColorDef Color(string id)
        {
            foreach (BladeColorDef c in Colors)
            {
                if (c.id == id) return c;
            }
            return null;
        }

        public static OrnamentDef Ornament(string id)
        {
            foreach (OrnamentDef o in Ornaments)
            {
                if (o.id == id) return o;
            }
            return null;
        }

        public static bool TryBuyColor(MetaProgress meta, BladeColorDef c)
        {
            if (meta.colors.Contains(c.id) || meta.petals < c.petals) return false;
            meta.petals -= c.petals;
            meta.colors.Add(c.id);
            meta.bladeColor = c.id;
            return true;
        }

        public static bool TryBuyOrnament(MetaProgress meta, OrnamentDef o)
        {
            if (meta.garden.Contains(o.id) || meta.petals < o.petals) return false;
            meta.petals -= o.petals;
            meta.garden.Add(o.id);
            return true;
        }

        public static void Apply(MetaProgress meta, PlayerModifiers m)
        {
            BladeColorDef c = string.IsNullOrEmpty(meta.bladeColor) ? null : Color(meta.bladeColor);
            if (c != null) m.bladeColor = c.color;
        }
    }
}
