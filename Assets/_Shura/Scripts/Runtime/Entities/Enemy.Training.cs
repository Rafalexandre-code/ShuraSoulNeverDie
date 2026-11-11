namespace Shura
{
    /// <summary>
    /// A training partner for the tutorial: it keeps its distance and its guard, and only
    /// strikes when the lesson asks for a blow — an honest cut to parry, or a red thrust to dodge.
    /// </summary>
    public sealed partial class Enemy
    {
        /// <summary>Never chooses to attack by itself.</summary>
        public bool Training { get; set; }
        MoveDef trainingMove;

        /// <summary>A blow was asked for, or is under way.</summary>
        public bool TrainingBusy => trainingMove != null || CurrentState == State.Attacking || CurrentState == State.Recoil;

        /// <summary>Asks for one blow: dangerous (red, to dodge) or honest (blue, to parry). It comes once in range.</summary>
        public bool RequestTraining(bool dangerous)
        {
            foreach (MoveDef m in moves)
            {
                if (m.steps.Length == 0 || m.steps[0].NoMeleeHit) continue;
                if (HasDangerousStep(m) != dangerous) continue;
                trainingMove = m;
                return true;
            }
            return false;
        }

        public void CancelTraining()
        {
            trainingMove = null;
        }

        void TickTraining(float dist)
        {
            if (trainingMove == null || dist > trainingMove.maxRange || dist < trainingMove.minRange) return;
            if (!world.Coordinator.TryAcquire(this)) return;
            MoveDef m = trainingMove;
            trainingMove = null;
            StartMove(m);
        }
    }
}
