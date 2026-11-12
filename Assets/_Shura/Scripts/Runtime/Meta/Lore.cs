namespace Shura
{
    /// <summary>The story the objects carry (design doc §8.7): every item remembers whose it was.</summary>
    public static class Lore
    {
        static readonly string[] Owners =
        {
            "um ashigaru que segurou a ponte sozinho", "uma sacerdotisa que lutava de olhos fechados", "um ronin que vendeu o nome por arroz",
            "um general que perdeu a guerra e ganhou o duelo", "um pescador que virou samurai por uma promessa", "uma kunoichi que nunca disse o próprio nome",
            "um velho mestre de dojo de aldeia", "um herdeiro que fugiu da própria coroação", "um carrasco que pedia perdão antes de cada golpe",
            "uma arqueira que contava as flechas em voz alta",
        };

        static readonly string[] Fates =
        {
            "morreu na neve sem soltar", "trocou pela vida de um irmão", "enterrou num campo de cerejeiras", "deixou num templo ao renascer",
            "perdeu num jogo de dados com uma raposa", "carregou por sete vidas seguidas", "jurou devolver e nunca voltou", "limpou do sangue de cem duelos",
        };

        /// <summary>One line of an item's past, always the same for the same item.</summary>
        public static string Item(Item item)
        {
            if (item == null) return "";
            int h = 23;
            foreach (char c in item.Name) h = h * 31 + c;
            h = (h ^ (int)item.slot * 7919 ^ (int)item.element * 104729) & 0x7FFFFFFF;
            string owner = Owners[h % Owners.Length];
            if (item.clan != Clan.None) owner += ", do clã " + ItemIdentity.ClanName(item.clan);
            string fate = Fates[(h / 11) % Fates.Length];
            string line = "Pertenceu a " + owner + ", que o " + fate + ".";
            if (item.corrupted) line += " Ainda está quente de rancor.";
            return line;
        }
    }
}
