using System;

namespace Iung.Animation
{
    /// <summary>
    /// Shared transform/visual channels used to coordinate transition and idle animations.
    /// </summary>
    [Flags]
    public enum UIAnimationChannels
    {
        None = 0,
        Position = 1 << 0,
        Scale = 1 << 1,
        Rotation = 1 << 2,
        Alpha = 1 << 3
    }
}
