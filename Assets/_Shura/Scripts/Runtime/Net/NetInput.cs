using System;
using UnityEngine;

namespace Shura
{
    /// <summary>A message from a browser player (parsed with JsonUtility; missing fields stay default).</summary>
    [Serializable]
    public sealed class NetIn
    {
        public string t;
        public string name;
        public string a;
        public string b;
        public float mx;
        public float my;
        public float ax;
        public float ay;
        /// <summary>Parry button held (block).</summary>
        public int p;
        /// <summary>Revive button held.</summary>
        public int r;
        /// <summary>Press counters: each increment is one press (robust against merged packets).</summary>
        public int atk;
        public int par;
        public int dod;
        public int swp;
        public double c;
        /// <summary>Lobby: chosen look (index into PlayerController.PartyArt), -1 = keep.</summary>
        public int art = -1;
        /// <summary>Reward picked, bag item to wear, or slot to empty (by message type).</summary>
        public int i = -1;
        /// <summary>Lobby: ready (1) or not (0); -1 = unchanged.</summary>
        public int rd = -1;
    }

    /// <summary>
    /// Input of a player in a browser, fed by their WebSocket messages. Presses arrive as
    /// counters and are buffered a little longer than local presses to absorb network jitter.
    /// </summary>
    public sealed class NetInput : IPlayerInputSource
    {
        const int BufferFrames = 9;

        Vector2 aim = Vector2.right;
        int lastAtk = -1, lastPar = -1, lastDod = -1, lastSwp = -1;

        public Vector2 Move { get; private set; }
        public bool ParryHeld { get; private set; }
        public bool InteractHeld { get; private set; }
        public InputBuffer Buffer { get; } = new InputBuffer();

        public Vector2 AimDirection(Vector2 playerWorldPosition) => aim;

        public void Apply(NetIn m)
        {
            var move = new Vector2(m.mx, m.my);
            Move = move.sqrMagnitude > 1f ? move.normalized : move;
            var a = new Vector2(m.ax, m.ay);
            if (a.sqrMagnitude > 0.01f) aim = a.normalized;
            ParryHeld = m.p != 0;
            InteractHeld = m.r != 0;
            Press(ref lastAtk, m.atk, BufferedAction.Attack);
            Press(ref lastPar, m.par, BufferedAction.Parry);
            Press(ref lastDod, m.dod, BufferedAction.Dodge);
            Press(ref lastSwp, m.swp, BufferedAction.Swap);
        }

        void Press(ref int last, int counter, BufferedAction action)
        {
            if (last >= 0 && counter != last) Buffer.Press(action, BufferFrames);
            last = counter;
        }

        /// <summary>Lost connection: let go of everything.</summary>
        public void Release()
        {
            Move = Vector2.zero;
            ParryHeld = false;
            InteractHeld = false;
            Buffer.Clear();
        }
    }
}
