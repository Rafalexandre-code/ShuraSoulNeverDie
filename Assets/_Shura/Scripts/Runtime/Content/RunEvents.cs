namespace Shura
{
    /// <summary>
    /// The narrative events of the road (design doc §6.7). Their choices live in
    /// <see cref="World"/> (BuildEvent); here are the words the party reads.
    /// </summary>
    public static class RunEvents
    {
        static readonly string[] Titles =
        {
            "O Monge Mendigo",
            "A Kitsune Disfarçada",
            "A Estátua de Jizō",
            "O Ronin Ferido",
            "Dados Chō-han",
            "O Onsen",
            "A Lâmina de Muramasa",
            "O Duelo de Honra",
        };

        static readonly string[] Texts =
        {
            "Um monge de tigela vazia bloqueia a ponte. \"Uma moeda pelo caminho, samurai?\"",
            "Uma mulher serve chá à beira da estrada. A sombra dela tem nove caudas.",
            "Um Jizō de pedra, coberto de musgo, guarda as almas dos viajantes. Há oferendas aos pés dele.",
            "Um ronin sangra encostado no muro. \"Me ajude… ou acabe com isso.\"",
            "Num canto, um jogador sacode o copo de dados. \"Chō ou han? Aposte o que tiver.\"",
            "Vapor sobe das pedras. A água quente promete esquecer tudo — inclusive a fúria e o Fluxo.",
            "Uma espada sem bainha, cravada no altar. Dizem que ela escolhe o dono… e o devora.",
            "Um samurai de armadura vermelha se curva. \"Sem truques. Só a lâmina.\"",
        };

        public static int Count => Titles.Length;
        public static string Title(int id) => Titles[((id % Count) + Count) % Count];
        public static string Text(int id) => Texts[((id % Count) + Count) % Count];
    }
}
