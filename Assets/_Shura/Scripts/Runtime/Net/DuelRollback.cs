using System;
using System.Collections.Generic;

namespace Shura
{
    /// <summary>
    /// Rollback netcode for a PvP duel (design doc §9.6): each side runs the whole duel at once,
    /// guessing the opponent's buttons (the same stick, no new presses) instead of waiting for
    /// them. When the real input of an earlier frame arrives and differs from the guess, the
    /// duel goes back to that frame and runs forward again — a one-frame parry stays one frame
    /// on both screens. Local input waits <see cref="Delay"/> frames (hiding most of the ping);
    /// beyond <see cref="MaxPredict"/> frames without news, the side waits instead of guessing.
    /// </summary>
    public sealed class DuelRollback
    {
        public const int Delay = 2;
        public const int MaxPredict = 10;
        const int Window = 64;
        public const int ChecksumEvery = 60;

        readonly DuelSide sideA, sideB;
        /// <summary>0 = this machine plays side A, 1 = side B.</summary>
        public readonly int Me;

        DuelState state;
        readonly DuelState[] saved = new DuelState[Window];
        readonly ushort[] local = new ushort[Window];
        readonly int[] localTag = new int[Window];
        readonly ushort[] remote = new ushort[Window];
        readonly int[] remoteTag = new int[Window];
        readonly ushort[] used = new ushort[Window];
        int rollbackFrom = -1;
        int confirmed = -1;
        ushort lastConfirmedRemote;
        int nextLocalFrame;
        readonly Dictionary<int, uint> myHashes = new Dictionary<int, uint>();
        readonly Dictionary<int, uint> theirHashes = new Dictionary<int, uint>();
        int lastHashed = -1;

        /// <summary>Called with (frame, input) for every local input: send it to the opponent.</summary>
        public Action<int, ushort> SendInput;
        /// <summary>Called with (frame, hash) every second of agreed duel: send it to the opponent.</summary>
        public Action<int, uint> SendHash;

        public DuelState State => state;
        public int Frame { get; private set; }
        /// <summary>The newest frame both inputs are known for (everything up to it is final).</summary>
        public int Confirmed => confirmed;
        /// <summary>The newest frame the opponent has sent (to keep the two clocks together).</summary>
        public int RemoteLatest { get; private set; } = -1;
        public int Rollbacks { get; private set; }
        public int RolledBackFrames { get; private set; }
        public bool Desynced { get; private set; }
        public int DesyncFrame { get; private set; } = -1;
        public int Stalls { get; private set; }

        public DuelRollback(DuelSide a, DuelSide b, int me)
        {
            sideA = a;
            sideB = b;
            Me = me;
            state = DuelSim.Start();
            for (int i = 0; i < Window; i++)
            {
                localTag[i] = -1;
                remoteTag[i] = -1;
            }
            // The first frames of input are nothing, on both sides, known by both.
            for (int f = 0; f < Delay; f++)
            {
                local[f] = 0;
                localTag[f] = f;
                remote[f] = 0;
                remoteTag[f] = f;
            }
            confirmed = Delay - 1;
            nextLocalFrame = Delay;
        }

        /// <summary>How many frames this side seems ahead of the opponent (the one-way ping included).</summary>
        public int Advantage => RemoteLatest < 0 ? 0 : Frame - (RemoteLatest - Delay);

        /// <summary>The opponent's own <see cref="Advantage"/>, as they last told it.</summary>
        public int RemoteAdvantage { get; set; }

        /// <summary>
        /// Both sides see the ping as "advantage"; only the difference says who truly runs ahead.
        /// Half of it is this side's lead: at one frame or more, it should wait now and then.
        /// </summary>
        public bool ShouldWait => (Advantage - RemoteAdvantage) / 2 >= 1;

        /// <summary>True when the next frame may be run now (not too far ahead of what the opponent sent).</summary>
        public bool CanAdvance => Frame - confirmed <= MaxPredict;

        /// <summary>
        /// One frame: the local input enters (for <see cref="Delay"/> frames later), any pending
        /// correction is replayed, and the duel steps once. Returns false when it had to wait.
        /// </summary>
        public bool Advance(ushort localInput)
        {
            Replay();
            if (!CanAdvance)
            {
                Stalls++;
                return false;
            }
            int f = nextLocalFrame++;
            local[f % Window] = localInput;
            localTag[f % Window] = f;
            SendInput?.Invoke(f, localInput);
            Simulate();
            Checksum();
            return true;
        }

        /// <summary>The opponent's input for one frame arrived (in any order, any number of times).</summary>
        public void Receive(int frame, ushort input)
        {
            if (frame < 0 || frame <= Frame - Window + 2) return;
            int i = frame % Window;
            if (remoteTag[i] == frame) return;
            remote[i] = input;
            remoteTag[i] = frame;
            if (frame > RemoteLatest) RemoteLatest = frame;
            // Already played with a guess: was the guess right?
            if (frame < Frame && used[i] != input && (rollbackFrom < 0 || frame < rollbackFrom)) rollbackFrom = frame;
            while (remoteTag[(confirmed + 1) % Window] == confirmed + 1)
            {
                confirmed++;
                lastConfirmedRemote = remote[confirmed % Window];
            }
        }

        public void ReceiveHash(int frame, uint hash)
        {
            theirHashes[frame] = hash;
            Compare(frame);
        }

        /// <summary>Applies a pending correction without moving on (tests, and the last frame of a duel).</summary>
        public void Settle() => Replay();

        void Replay()
        {
            if (rollbackFrom < 0) return;
            int from = rollbackFrom, to = Frame;
            rollbackFrom = -1;
            state = saved[from % Window];
            Frame = from;
            Rollbacks++;
            RolledBackFrames += to - from;
            while (Frame < to) Simulate();
        }

        void Simulate()
        {
            int f = Frame;
            int i = f % Window;
            saved[i] = state;
            ushort mine = localTag[i] == f ? local[i] : (ushort)0;
            ushort theirs = remoteTag[i] == f ? remote[i] : DuelInput.Predict(lastConfirmedRemote);
            used[i] = theirs;
            if (Me == 0) DuelSim.Step(ref state, mine, theirs, sideA, sideB);
            else DuelSim.Step(ref state, theirs, mine, sideA, sideB);
            Frame = f + 1;
        }

        /// <summary>Every second of agreed duel, both sides compare a fingerprint of the state.</summary>
        void Checksum()
        {
            // The state after frame k is final once every input up to k is known and no replay is pending.
            int k = confirmed - confirmed % ChecksumEvery;
            if (k <= lastHashed || k <= 0 || k + 1 > Frame - 1 || rollbackFrom >= 0) return;
            if (Frame - (k + 1) >= Window) return;
            lastHashed = k;
            uint h = DuelSim.Hash(saved[(k + 1) % Window]);
            myHashes[k] = h;
            SendHash?.Invoke(k, h);
            Compare(k);
        }

        void Compare(int frame)
        {
            if (!myHashes.TryGetValue(frame, out uint mine) || !theirHashes.TryGetValue(frame, out uint theirs)) return;
            if (mine != theirs && !Desynced)
            {
                Desynced = true;
                DesyncFrame = frame;
            }
            myHashes.Remove(frame);
            theirHashes.Remove(frame);
        }

        /// <summary>The duel is decided, and the deciding frame is beyond any doubt.</summary>
        public bool Finished => state.winner >= 0 && state.winFrame <= confirmed + 1 && rollbackFrom < 0;
    }
}
