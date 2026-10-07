using UnityEngine;

namespace LunarEscape
{
    // Main-scene surface bake only. The earlier lessons and global moon keep their original profile.
    // Render vertices, the walkable mesh and stone placement all sample this same metre-based height.
    public static class LunarSurfaceProfile
    {
        private readonly struct Crater
        {
            public readonly Vector2 Center;
            public readonly float Radius, Depth, Seed;
            public Crater(float x, float z, float radius, float depth, float seed)
            { Center = new Vector2(x, z); Radius = radius; Depth = depth; Seed = seed; }
        }

        private static readonly Crater[] OriginalNearCraters =
        {
            new(25,16,8,3.2f,1), new(44,-18,11,4,2), new(68,15,12,4.5f,3),
            new(10,-17,6,2.3f,4), new(77,-24,8,2.8f,5), new(-25,27,14,5,6),
            new(18,42,19,6,7), new(51,47,10,3,8)
        };
        private static readonly Crater[] SmallCraters =
        {
            new(21,8.5f,2.4f,.62f,11), new(18,-7,2.6f,.58f,12),
            new(32,-7.5f,1.8f,.42f,13), new(36,10,2.2f,.58f,14),
            new(42,-10,1.7f,.42f,15), new(58,-11,2.7f,.65f,16),
            new(60,7,1.6f,.38f,17), new(12,22,2.1f,.55f,18),
            new(-19,-18,3.1f,.72f,19), new(-22,8,2,.45f,20),
            new(6,32,2.5f,.58f,21), new(34,32,3.2f,.82f,22),
            new(49,28,1.8f,.4f,23), new(64,-29,2.8f,.68f,24),
            new(-36,4,3.6f,.82f,25), new(85,7,3.3f,.8f,26),
            new(7,-32,2.8f,.64f,27), new(31,-36,3.7f,.84f,28),
            new(-10,43,2.7f,.6f,29), new(64,41,2.4f,.52f,30)
        };

        public static float Height(float x, float z)
        {
            float original = LunarTerrainProfile.Height(x, z);
            float edge = Mathf.Min(96 - Mathf.Abs(x - 25), 80 - Mathf.Abs(z));
            float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(edge / 12)) * SafeAreaFreedom(x, z);
            if (weight <= 0) return original;
            var p = new Vector2(x, z);
            float detail = (Mathf.PerlinNoise(x * .47f + 24, z * .47f + 61) - .5f) * .16f;
            detail += (Mathf.PerlinNoise(x * .12f + 32, z * .12f + 16) - .5f) * .28f;
            foreach (var crater in OriginalNearCraters)
            {
                float q = Vector2.Distance(p, crater.Center) / crater.Radius;
                if (q > 1.8f) continue;
                float oldShape = q < 1 ? -crater.Depth * Mathf.Pow(1 - q * q, 2) : 0;
                oldShape += crater.Depth * .26f * Mathf.Exp(-Mathf.Pow((q - 1) / .15f, 2));
                detail += BrokenCrater(p, crater) - oldShape;
            }
            foreach (var crater in SmallCraters)
                if (Vector2.SqrMagnitude(p - crater.Center) < crater.Radius * crater.Radius * 2.6f)
                    detail += BrokenCrater(p, crater);
            return original + detail * weight;
        }

        private static float BrokenCrater(Vector2 p, Crater crater)
        {
            var delta = p - crater.Center;
            float angle = Mathf.Atan2(delta.y, delta.x);
            float irregular = 1 + .055f * Mathf.Sin(angle * 5 + crater.Seed) + .032f * Mathf.Sin(angle * 11 - crater.Seed * 1.7f);
            float q = delta.magnitude / crater.Radius * irregular;
            float bowl = q < 1 ? -crater.Depth * Mathf.Pow(1 - q * q, 1.55f) : 0;
            float fracture = .7f + .3f * Mathf.PerlinNoise(p.x * .8f + crater.Seed, p.y * .8f + 10);
            float rim = crater.Depth * .29f * fracture * Mathf.Exp(-Mathf.Pow((q - 1) / .085f, 2));
            // Broad, low ejecta apron connects the sharp rim to the old surface without an added lip.
            float apron = crater.Depth * .055f * Mathf.Exp(-Mathf.Pow((q - 1.17f) / .24f, 2));
            return bowl + rim + apron;
        }

        public static float SafeAreaFreedom(float x, float z)
        {
            float baseDistance = BaseDistance(x, z);
            float padDistance = Mathf.Max(0, Vector2.Distance(new Vector2(x, z), new Vector2(48, 1.7f)) - 7);
            float pathDistance = RouteDistance(x, z);
            return Mathf.Min(Mathf.SmoothStep(0, 1, baseDistance / 4),
                Mathf.Min(Mathf.SmoothStep(0, 1, padDistance / 4), Mathf.SmoothStep(0, 1, (pathDistance - 1.8f) / 3)));
        }

        public static float BaseDistance(float x, float z)
        {
            return new Vector2(Mathf.Max(0, Mathf.Abs(x + 2.5f) - 13.5f), Mathf.Max(0, Mathf.Abs(z) - 15)).magnitude;
        }

        public static float RouteDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            return Mathf.Min(SegmentDistance(p, new Vector2(7, 1.7f), new Vector2(38, 1.7f)),
                Mathf.Min(SegmentDistance(p, new Vector2(38, 1.7f), new Vector2(38, -4.3f)),
                    SegmentDistance(p, new Vector2(38, -4.3f), new Vector2(49, -4.3f))));
        }

        public static bool IsClearForRock(float x, float z, float horizontalRadius)
        {
            return BaseDistance(x, z) > horizontalRadius + 1.4f &&
                Vector2.Distance(new Vector2(x, z), new Vector2(48, 1.7f)) > 8 + horizontalRadius &&
                RouteDistance(x, z) > 3.6f + horizontalRadius;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            return Vector2.Distance(p, a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude));
        }
    }
}
