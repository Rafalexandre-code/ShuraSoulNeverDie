using System;
using UnityEngine;

namespace Shura
{
    /// <summary>A guest of the Tea House (design doc §8.2): who they are, what they ask, what they give.</summary>
    public sealed class GuestDef
    {
        public string id;
        public string name;
        public string role;
        /// <summary>The character art that sits at the table.</summary>
        public string art;
        public string kanji;
        public Color color;
        public string mission;
        public string reward;
        /// <summary>Progress toward the mission (compared with <see cref="goal"/>).</summary>
        public Func<MetaProgress, int> progress;
        public int goal;
        // ── What they say ──
        public string[] greetings;
        /// <summary>After a fall; {0} = who struck the blow.</summary>
        public string afterDeath;
        /// <summary>After a journey that came home; {0} = the disciple, {1} = the age.</summary>
        public string afterJourney;
        public string bushido;
        public string shura;
        public string ready;
        public string thanks;
    }

    /// <summary>
    /// The six guests of the Tea House. Each one speaks differently after your last life and
    /// with your Karma, and keeps a personal mission whose reward opens a mechanic.
    /// </summary>
    public static class TeaGuests
    {
        public const string Smith = "ferreiro";
        public const string Kohaku = "kohaku";
        public const string Zato = "zato";
        public const string Tsubaki = "tsubaki";
        public const string Warashi = "warashi";
        public const string Monk = "monge";
        public const int MonkPoints = 2;

        // Lifetime counters (MetaProgress.stats).
        public const string StatSteel = "aco_total";
        public const string StatFoxes = "raposas";
        public const string StatParries = "parries";
        public const string StatHonor = "duelos_honra";
        public const string StatPrayers = "oracoes";
        public const string StatNoto = "noto";
        public const string StatBows = "reverencias";

        public static readonly GuestDef[] All =
        {
            new GuestDef
            {
                id = Smith, name = "O Ferreiro sem Rosto", role = "Espírito de um forjador de lâminas malditas. Cuida da Forja.",
                art = "NinjaMasked", kanji = "鍛", color = ColorUtil.Hex("#9FE7FF"),
                mission = "Traga ao templo 12 Aço Celeste, somando todas as vidas.",
                reward = "Tsubas lendárias na Forja: Ryūjin, Muramasa e Vazio.",
                progress = m => m.Stat(StatSteel), goal = 12,
                greetings = new[]
                {
                    "…O aço lembra de cada mão que o segurou. A sua ainda treme.",
                    "Não tenho rosto. Deixei-o na última lâmina que forjei. Ela ainda corta, em algum lugar.",
                    "O fogo da forja não se apaga. Nem o meu arrependimento.",
                },
                afterDeath = "{0}. Conheço o golpe. Uma boa lâmina teria aguentado. Traga-me aço.",
                afterJourney = "{0} voltou com a lâmina inteira. {1} anos… o metal envelhece melhor que a carne.",
                bushido = "Você limpa a lâmina antes de embainhar. Poucos ainda respeitam o aço assim.",
                shura = "Sinto cheiro de Muramasa em você. Cuidado: foi assim que eu perdi o rosto.",
                ready = "Isto basta. Sente-se. Vou lhe mostrar as tsubas que nunca deveria ter forjado.",
                thanks = "As guardas lendárias esperam na Forja. Use-as melhor do que eu usei.",
            },
            new GuestDef
            {
                id = Kohaku, name = "Kohaku, a Kitsune", role = "Mercadora que às vezes mente. Vende relíquias à beira da estrada.",
                art = "Shaman", kanji = "狐", color = ColorUtil.Hex("#FFB067"),
                mission = "Desmascare a raposa disfarçada na estrada 2 vezes.",
                reward = "Na estrada, você passa a ver quando o chá dela é maldito.",
                progress = m => m.Stat(StatFoxes), goal = 2,
                greetings = new[]
                {
                    "Mais chá? Este é de verdade. …Provavelmente.",
                    "Nove caudas, nove histórias. Hoje eu conto a sexta.",
                    "Você olha para a minha sombra antes de olhar para mim. Esperto.",
                },
                afterDeath = "Ah, {0}. Eu teria avisado… se você tivesse comprado alguma coisa.",
                afterJourney = "{0}, {1} anos e ainda com todos os dedos. Sorte não se vende, mas eu tentaria.",
                bushido = "Tão honrado… é quase tentador mentir para você só para ver a cara que faz.",
                shura = "Os olhos vermelhos lhe caem bem. Mas raposas sabem quando correr.",
                ready = "Você me desmascarou duas vezes. Está bem: daqui em diante, eu pisco quando mentir.",
                thanks = "Quando o chá for maldito, você vai saber. Não conte às outras raposas.",
            },
            new GuestDef
            {
                id = Zato, name = "Mestre Zatō", role = "Velho espadachim cego. Cuida do Dojo.",
                art = "Master", kanji = "盲", color = ColorUtil.Hex("#E9E1C9"),
                mission = "Faça 150 parries perfeitos, somando todas as vidas.",
                reward = "O Modo Mestre: sem anéis de tempo, só o som e o corpo do inimigo.",
                progress = m => m.Stat(StatParries), goal = 150,
                greetings = new[]
                {
                    "Eu não vejo os anéis. Nunca vi. A lâmina avisa pelo vento que empurra.",
                    "Feche os olhos quando ouvir o aço sair da bainha. Conte. Um. Agora.",
                    "Seus passos estão mais leves. Ou você emagreceu de tanto morrer.",
                },
                afterDeath = "{0}… Você viu o golpe chegar e ainda assim olhou para o anel. Escute mais.",
                afterJourney = "{0}, {1} anos. Ouço os joelhos estalarem. Isso é bom: o corpo aprendeu algo.",
                bushido = "Há silêncio no seu coração. É o primeiro passo do Mushin.",
                shura = "Você respira como um animal ferido. O Shura grita alto; eu prefiro quem sussurra.",
                ready = "Cento e cinquenta. Agora tire a venda que nunca usou e lute como eu luto.",
                thanks = "O Modo Mestre espera em \"Modos de jogo\". Sem anel. Só o som.",
            },
            new GuestDef
            {
                id = Tsubaki, name = "Tsubaki", role = "Outra alma presa que também renasce. Rival amigável dos duelos.",
                art = "NinjaRed", kanji = "椿", color = ColorUtil.Hex("#E0465A"),
                mission = "Vença 3 Duelos de Honra na estrada.",
                reward = "Os Duelos de Honra aparecem mais e valem +1 de Karma e 5 pétalas.",
                progress = m => m.Stat(StatHonor), goal = 3,
                greetings = new[]
                {
                    "Morri trezentas e doze vezes. E você? Ah, ainda está contando.",
                    "Um dia a gente se encontra numa sala de duelo. Não vou pegar leve.",
                    "As camélias caem inteiras, sabia? Sem perder uma pétala. Quero morrer assim.",
                },
                afterDeath = "{0}? Eu caí para o mesmo golpe na minha quinquagésima vida. Fica mais fácil. Mentira, não fica.",
                afterJourney = "{0} chegou longe! {1} anos. Eu tinha essa idade quando parei de envelhecer.",
                bushido = "Você se curva antes do impasse. Isso me faz querer vencer você direito.",
                shura = "Não gosto do que está virando. Lembre-se do rosto de quem foi antes da roda.",
                ready = "Três duelos limpos. Então é verdade: você luta com honra. Vou espalhar a notícia.",
                thanks = "Os duelos de honra vão te procurar. Eu também, um dia.",
            },
            new GuestDef
            {
                id = Warashi, name = "Zashiki-warashi", role = "Espírito criança que guarda o Bestiário.",
                art = "Spirit", kanji = "童", color = ColorUtil.Hex("#C9A0FF"),
                mission = "Conheça 12 inimigos diferentes (Biblioteca).",
                reward = "Os pergaminhos de inimigo: cinco desafios para cada um.",
                progress = m => m.KnownEnemies, goal = 12,
                greetings = new[]
                {
                    "Eu desenho todos que entram. Você tem um nariz difícil.",
                    "Os monstros também têm nome, sabia? Ninguém pergunta.",
                    "Se você ouvir passos pequenos na Biblioteca, sou eu. Ou não.",
                },
                afterDeath = "Desenhei o {0} ontem. Ele estava sorrindo. Agora sei por quê.",
                afterJourney = "{0} voltou com {1} anos e cheio de histórias! Conta, conta!",
                bushido = "A casa fica quente quando você chega. Casas sabem quem é bom.",
                shura = "Você está com cheiro de pesadelo. Vou desenhar você de longe hoje.",
                ready = "Doze! O livro está cheio de desenhos. Agora quero que você os entenda de verdade.",
                thanks = "Os pergaminhos estão na Biblioteca. Cada inimigo esconde cinco segredos.",
            },
            new GuestDef
            {
                id = Monk, name = "O Monge Sem Nome", role = "Guarda o Altar e explica o Shuradō.",
                art = "Monk", kanji = "僧", color = ColorUtil.Hex("#F2C14E"),
                mission = "Reze 5 vezes diante de estátuas e lanternas (segure Interagir perto delas, com a sala em paz).",
                reward = "+2 pontos no Altar das Almas.",
                progress = m => m.Stat(StatPrayers), goal = 5,
                greetings = new[]
                {
                    "O Shuradō é o reino onde guerreiros lutam para sempre. Você ainda acha que é um lugar.",
                    "Os Ecos são memórias. Cada vida esquece um pouco menos.",
                    "Quem absorve uma técnica leva um pedaço de alma. Pergunte-se quantos pedaços você já carrega.",
                },
                afterDeath = "{0} não foi seu inimigo. Foi seu professor. Agradeça na próxima vida.",
                afterJourney = "{0}, {1} anos. A roda gira mais devagar para quem aprende.",
                bushido = "Há quem saia da roda pela porta da compaixão. Você está perto dela.",
                shura = "O trono do Ashura está vazio de paz e cheio de almas. Não sente a cadeira chamando?",
                ready = "Você rezou sem pedir nada. É raro. O Altar vai se abrir um pouco mais para você.",
                thanks = "Dois pontos a mais no Altar. Use-os para ser alguém, não alguma coisa.",
            },
        };

        public static GuestDef Get(string id)
        {
            if (id == null) return null;
            foreach (GuestDef g in All)
            {
                if (g.id == id) return g;
            }
            return null;
        }

        public static int Progress(MetaProgress m, GuestDef g) => Mathf.Min(g.goal, g.progress(m));

        public static bool Ready(MetaProgress m, GuestDef g) => !m.MissionDone(g.id) && g.progress(m) >= g.goal;

        public static bool TryClaim(MetaProgress m, GuestDef g)
        {
            if (!Ready(m, g)) return false;
            m.missions.Add(g.id);
            return true;
        }

        /// <summary>
        /// What the guest says now: the mission first (ready or just done), then your last
        /// life, then your Karma, then a greeting that changes from run to run.
        /// </summary>
        public static string Line(MetaProgress m, GuestDef g, LegacyReport last)
        {
            if (Ready(m, g)) return g.ready;
            if (last != null && last.disciple != null)
            {
                if (last.died && !string.IsNullOrEmpty(last.disciple.cause)) return string.Format(g.afterDeath, last.disciple.cause);
                if (last.aged) return string.Format(g.afterJourney, last.disciple.name, last.disciple.age);
            }
            if (m.karma >= 40) return g.bushido;
            if (m.karma <= -40) return g.shura;
            int i = Array.IndexOf(All, g);
            return g.greetings[(m.runs + i) % g.greetings.Length];
        }
    }
}
