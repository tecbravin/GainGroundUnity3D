using UnityEngine;

namespace GainGround.Game
{
    // Deterministic geometry shared by movement and projectile collision.
    public static class ArenaRules
    {
        public static bool Intersects(Rect box, Vector2 point, float radius)
        {
            var closest = new Vector2(Mathf.Clamp(point.x, box.xMin, box.xMax), Mathf.Clamp(point.y, box.yMin, box.yMax));
            return (closest - point).sqrMagnitude <= radius * radius;
        }

        public static bool Blocked(Vector2 point, float radius, Vector2 size, Rect[] walls)
        {
            if (Mathf.Abs(point.x) + radius > size.x / 2 || Mathf.Abs(point.y) + radius > size.y / 2) return true;
            foreach (var wall in walls) if (Intersects(wall, point, radius)) return true;
            return false;
        }

        public static bool SegmentIntersects(Rect box, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float enter = 0f, leave = 1f;
            if (!ClipAxis(from.x, delta.x, box.xMin, box.xMax, ref enter, ref leave)) return false;
            return ClipAxis(from.y, delta.y, box.yMin, box.yMax, ref enter, ref leave);
        }

        static bool ClipAxis(float origin, float delta, float min, float max, ref float enter, ref float leave)
        {
            if (Mathf.Abs(delta) < .00001f) return origin >= min && origin <= max;
            float near = (min - origin) / delta;
            float far = (max - origin) / delta;
            if (near > far) { float swap = near; near = far; far = swap; }
            enter = Mathf.Max(enter, near);
            leave = Mathf.Min(leave, far);
            return enter <= leave;
        }

        public static Vector2 Move(Vector2 from, Vector2 delta, float radius, Vector2 size, Rect[] walls)
        {
            // Small steps prevent tunnelling on slow frames; axes separately allow wall sliding.
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.1f));
            delta /= steps;
            for (int i = 0; i < steps; i++)
            {
                var next = from + new Vector2(delta.x, 0);
                if (!Blocked(next, radius, size, walls)) from = next;
                next = from + new Vector2(0, delta.y);
                if (!Blocked(next, radius, size, walls)) from = next;
            }
            return from;
        }
    }
}
