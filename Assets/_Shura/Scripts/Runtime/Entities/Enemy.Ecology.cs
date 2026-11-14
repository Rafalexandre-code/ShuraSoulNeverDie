using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Phase 5, part 3 on the enemy: the new enemies' own rules (segments, natures, the
    /// finisher-only blade, the blind duelist, the faceless mimic, the mirror), the Kappa's
    /// fear of fire, sealed red blows, and the bonuses of a formation.
    /// </summary>
    public sealed partial class Enemy
    {
        int segmentsLeft = -1;
        int natureIndex;
        int dangerSealed;
        int scaredFrames;
        /// <summary>Phalanx: a spearman behind a living shield takes far less.</summary>
        public bool FormationGuarded { get; set; }
        /// <summary>The Ōmukade's segments still standing (0 for everyone else).</summary>
        public int SegmentsLeft => Def.segments <= 0 ? 0 : segmentsLeft < 0 ? Def.segments : segmentsLeft;

        /// <summary>Selo Proibido: no red (dangerous) blows for a while.</summary>
        public void SealDanger(int frames)
        {
            dangerSealed = Mathf.Max(dangerSealed, frames);
            world.Fx.Popup(Strings.PopupDangerSealed, Position + Vector2.up * 1.3f, pal.gold);
        }

        /// <summary>The Kappa runs from fire.</summary>
        public void Scare(int frames)
        {
            if (scaredFrames <= 0) world.Fx.Popup(Strings.PopupScared, Position + Vector2.up * 1.3f, pal.uiDim);
            scaredFrames = Mathf.Max(scaredFrames, frames);
            world.Coordinator.Release(this);
            CurrentMove = null;
            if (CurrentState == State.Attacking) Enter(State.Engage);
        }

        void TickEcologyTimers()
        {
            if (dangerSealed > 0) dangerSealed--;
        }

        /// <summary>Running from the fire instead of fighting (returns true when it handled the tick).</summary>
        bool TickScared()
        {
            if (scaredFrames <= 0 || IsGone || CurrentState != State.Engage) return false;
            scaredFrames--;
            PlayerController near = world.NearestAlivePlayer(Position);
            Vector2 away = near != null ? Position - near.Position : -Facing;
            if (away.sqrMagnitude < 1e-4f) away = Vector2.up;
            Velocity = world.Layout.Steer(Position, Position + away.normalized * 4f, Radius) * Def.moveSpeed * 1.2f;
            Facing = away.normalized;
            Integrate();
            return true;
        }

        /// <summary>Ōmukade: a broken posture costs a segment, not the fight (until the last one).</summary>
        bool SegmentBreak()
        {
            if (Def.segments <= 0) return false;
            if (segmentsLeft < 0) segmentsLeft = Def.segments;
            if (segmentsLeft <= 1) return false;
            segmentsLeft--;
            Posture.Reset(0f);
            world.Coordinator.Release(this);
            CurrentMove = null;
            staggerFrames = 50;
            Enter(State.Stagger);
            Squash(1.25f, 0.75f);
            world.Fx.Popup(string.Format(Strings.PopupSegment, segmentsLeft), Position + Vector2.up * 1.3f, pal.gold);
            world.Fx.Emit(FxType.PostureBreak, Position, Facing, Radius * 0.7f, Def.color);
            return true;
        }

        /// <summary>Nue: every perfect parry it suffers turns it into its next nature.</summary>
        void NextNature()
        {
            if (Def.natures == null || Def.natures.Length < 2) return;
            natureIndex = (natureIndex + 1) % Def.natures.Length;
            moves = Def.natures[natureIndex];
            world.Fx.Popup(Def.natureNames[natureIndex].ToUpperInvariant(), Position + Vector2.up * 1.4f, Def.color);
        }

        /// <summary>Nopperabō: it fights with the moves of whoever fell last.</summary>
        public void Mimic(EnemyDef fallen)
        {
            if (!Def.mimic || fallen == null || fallen.moves == null || fallen.moves.Length == 0 || IsGone) return;
            moves = fallen.moves;
            world.Fx.Popup(string.Format(Strings.PopupMimic, fallen.displayName), Position + Vector2.up * 1.3f, pal.uiDim);
        }

        /// <summary>The Shura's mirror: a duelist's own combo against them.</summary>
        public void MakeMirror(PlayerController of)
        {
            if (of == null) return;
            MirrorMoves(EnemyDefs.InnerAsura(of));
        }

        /// <summary>The mirror takes a duelist's combo and keeps its own signature.</summary>
        public void MirrorMoves(EnemyDef copy)
        {
            if (copy == null) return;
            var list = new System.Collections.Generic.List<MoveDef>(copy.moves);
            foreach (MoveDef m in Def.moves)
            {
                if (m.isSignature) list.Add(m);
            }
            moves = list.ToArray();
        }

        /// <summary>The new Ashura: a friend's combo joins the King's own.</summary>
        public void AddMoves(MoveDef[] extra)
        {
            if (extra == null || extra.Length == 0) return;
            var list = new System.Collections.Generic.List<MoveDef>(moves);
            list.AddRange(extra);
            moves = list.ToArray();
        }

        /// <summary>The boss banner's name when a friend became the Ashura.</summary>
        public string BossNameOverride { get; set; }

        /// <summary>The Hunt: the shadow appears at the duelist's back and strikes.</summary>
        public void AppearBehind(PlayerController t)
        {
            if (IsGone || t == null) return;
            world.Fx.Emit(FxType.Smoke, Position, Vector2.up, Radius * 1.4f, Def.color);
            Vector2 back = t.Facing.sqrMagnitude > 1e-4f ? -t.Facing : Vector2.down;
            Teleport(world.Layout.NearestFloor(t.Position + back * 1.4f, Radius));
            Facing = (t.Position - Position).normalized;
            world.Fx.Emit(FxType.ShadowStep, Position, Facing, Radius, Def.color);
            Provoke();
        }

        /// <summary>The blind duelist only knows where you are when you make a sound.</summary>
        bool CanHearTarget()
        {
            if (!Def.blind) return true;
            PlayerController t = Target != null ? Target : world.Host;
            return t == null || !t.Quiet || Vector2.Distance(t.Position, Position) < 1.6f;
        }
    }
}
