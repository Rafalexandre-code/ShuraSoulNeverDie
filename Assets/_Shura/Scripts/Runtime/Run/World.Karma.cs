using UnityEngine;

namespace Shura
{
    /// <summary>
    /// WP-07: o Karma visível. Cada ação de Karma (poupar, dar ao monge, executar quem se ajoelha,
    /// trair um rival…) acende um kanji sobre o samurai — 義 (Bushidō) ou 修 (Shura) — com som, e a
    /// tela de morte mostra a linha do Karma da run. Antes ele mudava em silêncio.
    /// </summary>
    public sealed partial class World
    {
        public void ChangeKarma(int delta, Vector2? at = null)
        {
            if (Run == null || delta == 0) return;
            Run.Karma += delta;
            PlayerController p = Host ?? Player;
            Vector2 where = at ?? (p != null ? p.Position : Vector2.zero);
            Fx.Emit(FxType.Karma, where + Vector2.up * 1.7f, Vector2.up, delta);
        }
    }
}
