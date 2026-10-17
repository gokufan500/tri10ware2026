using UnityEngine;

namespace CatCannonball
{
    // hi physics majors
    public static class MovementMath
    {
        public static Vector2 DragForce(Vector2 velocity, float coefficient, float mass, float step)
        {
            if (velocity.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            float force = Mathf.Min(Mathf.Max(0f, coefficient) * velocity.sqrMagnitude, velocity.magnitude * mass / Mathf.Max(step, 0.0001f));
            return -velocity.normalized * force;
        }

        public static Vector2 SpaceReleaseVelocity(Vector2 velocity, MovementSettings settings)
        {
            float multiplier = settings.SpaceStationaryMultiplier;
            if (velocity.magnitude > Mathf.Max(0f, settings.SpaceStationarySpeed))
            {
                float downwardAlignment = Vector2.Dot(velocity.normalized, Vector2.down);
                multiplier = settings.SpaceDirectionalMultiplier == null ? settings.SpaceStationaryMultiplier : settings.SpaceDirectionalMultiplier.Evaluate(downwardAlignment);
            }

            return velocity + Vector2.up * (Mathf.Max(0f, settings.SpaceReleaseBump) * Mathf.Max(0f, multiplier));
        }

        public static Vector2 YankVelocity(Vector2 velocity, Vector2 towardPivot, MovementSettings settings, out bool lowSpeedYank)
        {
            float speed = velocity.magnitude;
            lowSpeedYank = speed < Mathf.Max(0f, settings.YankSpeedThreshold);
            if (towardPivot.sqrMagnitude < 0.000001f)
            {
                return velocity;
            }

            Vector2 pivotDirection = towardPivot.normalized;
            if (lowSpeedYank)
            {
                return velocity + pivotDirection * Mathf.Max(0f, settings.YankVelocityChange);
            }

            float dominance = Mathf.Clamp(settings.RedirectPivotDominance, 0.51f, 1f);
            Vector2 direction = pivotDirection * dominance;
            if (speed > 0.00001f)
            {
                direction += (velocity / speed) * (1f - dominance);
            }

            return direction.normalized * speed;
        }
    }
}
