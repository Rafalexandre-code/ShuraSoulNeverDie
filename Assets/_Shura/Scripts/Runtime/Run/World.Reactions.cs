using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Elemental reactions (design doc §4.7). Carrying two weapons, items or tools of different
    /// elements now pays: the second element on an enemy marked by the first sets one off.
    /// </summary>
    public sealed partial class World
    {
        static bool Pair(Element a, Element b, Element x, Element y) => (a == x && b == y) || (a == y && b == x);

        /// <summary>Returns false when the two elements do not react (the new one simply replaces the old).</summary>
        public bool ElementalReaction(Enemy e, Element a, Element b)
        {
            if (!Has(Unlockable.Reactions)) return false;
            Palette pal = Config.palette;
            Color c;
            string name;
            if (Pair(a, b, Element.Fire, Element.Frost))
            {
                // Vapor: a blinding cloud; those inside lose their target.
                name = Strings.ReactionSteam;
                c = ColorUtil.Hex("#DDE6EE");
                Fx.Emit(FxType.Smoke, e.Position, Vector2.up, 2.2f, c);
                foreach (Enemy o in Enemies)
                {
                    if (o.CanBeHit && !o.IsAlly && Vector2.Distance(o.Position, e.Position) <= 2.6f) o.Confuse(150);
                }
            }
            else if (Pair(a, b, Element.Frost, Element.Lightning))
            {
                // Estilhaço: the frozen body cracks — a chunk of posture at once.
                name = Strings.ReactionShatter;
                c = Elements.Color(Element.Frost);
                e.ApplyPosture(60f);
                Fx.Emit(FxType.SlamImpact, e.Position, Vector2.up, 1.6f, c);
            }
            else if (Pair(a, b, Element.Fire, Element.Wind))
            {
                // Tornado Ígneo: a whirl of fire pulls everyone near in and burns them.
                name = Strings.ReactionFireTornado;
                c = Elements.Color(Element.Fire);
                foreach (Enemy o in Enemies)
                {
                    if (!o.CanBeHit || o.IsAlly || Vector2.Distance(o.Position, e.Position) > 3.4f) continue;
                    if (o != e) o.Pull(e.Position, 2.5f);
                    o.ApplyDot(7f, 3f, Element.Fire, false);
                }
                Fx.Emit(FxType.Burst, e.Position, Vector2.up, 3.4f, c);
                Fx.Emit(FxType.Explosion, e.Position, Vector2.up, 1.6f, c);
            }
            else if (Pair(a, b, Element.Blood, Element.Shadow))
            {
                // Maldição: for a while, every blow it starts heals you.
                name = Strings.ReactionCurse;
                c = ColorUtil.Hex("#9B2335");
                e.CurseHeal(Player, 6 * SimClock.TickRate);
                Fx.Emit(FxType.Curse, e.Position, Vector2.up, 1f, c);
            }
            else if (Pair(a, b, Element.Lightning, Element.Blood))
            {
                // Sobrecarga: the bleeding leaps to the nearest enemies.
                name = Strings.ReactionOverload;
                c = Elements.Color(Element.Lightning);
                int jumps = 0;
                foreach (Enemy o in Enemies)
                {
                    if (jumps >= 3 || o == e || !o.CanBeHit || o.IsAlly || Vector2.Distance(o.Position, e.Position) > 5.5f) continue;
                    Vector2 dir = o.Position - e.Position;
                    Fx.Emit(FxType.Lightning, e.Position, dir.normalized, dir.magnitude, c);
                    o.ApplyDot(6f, 4f, Element.Blood, false);
                    jumps++;
                }
                e.ApplyDot(6f, 4f, Element.Blood, false);
            }
            else if (Pair(a, b, Element.Frost, Element.Wind))
            {
                // Nevasca: a cone of snow freezes everyone in it for a breath and a half.
                name = Strings.ReactionBlizzard;
                c = Elements.Color(Element.Frost);
                Vector2 from = Player.Position;
                Vector2 dir = e.Position - from;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Player.Facing;
                HitShape cone = HitShape.Arc(5f, 80f);
                foreach (Enemy o in Enemies)
                {
                    if (!o.CanBeHit || o.IsAlly || !CombatMath.Overlaps(cone, from, dir, o.Position, o.Radius)) continue;
                    o.Stun(90);
                    o.ApplyChill(2.5f, false);
                }
                Fx.Emit(FxType.Burst, e.Position, dir, 3f, c);
            }
            else return false;

            Fx.Popup(name, e.Position + Vector2.up * 1.4f, c);
            Fx.Emit(FxType.Burst, e.Position, Vector2.up, 1.4f, c);
            HintOnce(HintId.Reactions);
            return true;
        }
    }
}
