using UnityEngine;

namespace Shura
{
    public static class CombatMath
    {
        /// <summary>Does an attack shape anchored at origin, pointing along facing, touch a target circle?</summary>
        public static bool Overlaps(HitShape shape, Vector2 origin, Vector2 facing, Vector2 target, float targetRadius)
        {
            Vector2 d = target - origin;
            float dist = d.magnitude;
            switch (shape.type)
            {
                case HitShapeType.Circle:
                    return dist <= shape.range + targetRadius;

                case HitShapeType.Arc:
                {
                    if (dist > shape.range + targetRadius) return false;
                    if (dist < 0.0001f) return true;
                    // Widen the arc by the angle the target's body subtends, so grazing hits count.
                    float tolerance = Mathf.Atan2(targetRadius, dist) * Mathf.Rad2Deg;
                    return Vector2.Angle(facing, d) <= shape.arcDegrees * 0.5f + tolerance;
                }

                case HitShapeType.Line:
                {
                    float along = Vector2.Dot(d, facing);
                    if (along < -targetRadius || along > shape.range + targetRadius) return false;
                    float perpendicular = Mathf.Abs(d.x * facing.y - d.y * facing.x);
                    return perpendicular <= shape.width * 0.5f + targetRadius;
                }
            }
            return false;
        }

        public static bool WithinArc(Vector2 facing, Vector2 toTarget, float arcDegrees)
        {
            if (toTarget.sqrMagnitude < 1e-6f) return true;
            return Vector2.Angle(facing, toTarget) <= arcDegrees * 0.5f;
        }

        public static Vector2 RotateTowards(Vector2 from, Vector2 to, float maxDegrees)
        {
            if (to.sqrMagnitude < 1e-6f) return from;
            float a = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float b = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float r = Mathf.MoveTowardsAngle(a, b, maxDegrees) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public static Vector2 Perpendicular(Vector2 v)
        {
            return new Vector2(-v.y, v.x);
        }

        public static float AngleDegrees(Vector2 v)
        {
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        public static float EaseOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>Frame-rate independent exponential smoothing factor.</summary>
        public static float Damp(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-sharpness * dt);
        }
    }
}
