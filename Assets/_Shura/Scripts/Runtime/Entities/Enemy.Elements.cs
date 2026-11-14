using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Elements on an enemy (design doc §4.7): the last element that touched it stays for a
    /// few seconds; a different one on top of it sets off a reaction. Plain steel makes the
    /// element already there last twice as long.
    /// </summary>
    public sealed partial class Enemy
    {
        const int ElementFrames = 240;

        public Element ActiveElement { get; private set; }
        int elementFrames;
        bool resonated;
        /// <summary>Maldição: every blow it starts heals the duelist who cursed it.</summary>
        int cursedHealFrames;
        PlayerController cursedBy;

        public float ElementRatio => ActiveElement == Element.None ? 0f : Mathf.Clamp01(elementFrames / (float)ElementFrames);

        /// <summary>An element touches this enemy: it stays, or reacts with the one already there.</summary>
        public void MarkElement(Element e)
        {
            if (!CanBeHit || e == Element.None) return;
            // The Kappa runs from fire (§5.3).
            if (e == Element.Fire && Def.id == "kappa") Scare(120);
            if (elementFrames > 0 && ActiveElement != Element.None && ActiveElement != e)
            {
                Element first = ActiveElement;
                ActiveElement = Element.None;
                elementFrames = 0;
                if (world.ElementalReaction(this, first, e)) return;
            }
            ActiveElement = e;
            elementFrames = ElementFrames;
            resonated = false;
        }

        /// <summary>Ressonância: steel on an element doubles how long it lasts (once per element).</summary>
        public void Resonate()
        {
            if (resonated || elementFrames <= 0 || ActiveElement == Element.None) return;
            resonated = true;
            elementFrames *= 2;
            dotTime *= 2f;
            chillTime *= 2f;
            world.Fx.Popup(Strings.ReactionResonance, Position + Vector2.up * 1.2f, Elements.Color(Element.None));
            world.Fx.Emit(FxType.Burst, Position, Vector2.up, Radius * 2.2f, Elements.Color(ActiveElement));
        }

        public void CurseHeal(PlayerController by, int frames)
        {
            cursedBy = by;
            cursedHealFrames = Mathf.Max(cursedHealFrames, frames);
        }

        void TickElement()
        {
            if (elementFrames > 0 && --elementFrames == 0) ActiveElement = Element.None;
            if (cursedHealFrames > 0) cursedHealFrames--;
        }

        /// <summary>A cursed enemy's blow feeds the one who cursed it.</summary>
        void OnMoveStartedCursed()
        {
            if (cursedHealFrames <= 0 || cursedBy == null || !cursedBy.IsAlive) return;
            cursedBy.Heal(5f);
            world.Fx.Emit(FxType.Drain, cursedBy.Position, (cursedBy.Position - Position).normalized, 5f, Elements.Color(Element.Blood));
        }
    }
}
