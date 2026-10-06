namespace Iung.Tools.Shared.Pooling
{
    // AI GUIDANCE:
    // - When to use: when pooled components need an explicit contract to reset transient runtime state before reuse.
    // - Prefer: implementing IPoolResettable on reusable components that must clear internal state when returning from a pool.
    // - Avoid: relying only on Awake or Start to reset pooled objects, since those callbacks may not run on each reuse cycle.
    // - Reason: this interface integrates cleanly with PoolStateRestorer automatic ResetForReuse dispatch.
    public interface IPoolResettable
    {
        /// <summary>
        /// Restores transient runtime data to a reusable baseline state.
        /// </summary>
        void ResetForReuse();
    }
}
