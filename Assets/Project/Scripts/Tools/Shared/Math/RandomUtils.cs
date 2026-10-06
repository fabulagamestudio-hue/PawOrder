using UnityEngine;

namespace Iung.Tools.Shared.Math
{
    // AI GUIDANCE:
    // - When to use: when sampling values from Vector2 ranges or generating a random index from a max value.
    // - Prefer: using RandomUtils extension methods to keep random range usage consistent and readable across systems.
    // - Avoid: scattering manual casts and Random.Range wrappers when RandomUtils already covers the same intent.
    // - Reason: Vector2 is treated as min and max boundaries, so range configuration order matters.
    public static class RandomUtils
    {
        public static float RandomInRange(this Vector2 range)
        {
            return Random.Range(range.x, range.y);
        }

        public static int RandomIntInRange(this Vector2 range)
        {
            return Random.Range((int)range.x, (int)range.y);
        }

        public static int RandomIndex(this int max)
        {
            return Random.Range(0, max);
        }
    }
}
