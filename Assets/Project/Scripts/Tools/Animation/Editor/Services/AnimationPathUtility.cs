using System.IO;

namespace Iung.Animation
{
    public static class AnimationPathUtility
    {
        public static string NormalizeAssetPath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            string normalizedPath = rawPath.Trim().Replace('\\', '/');
            while (normalizedPath.Contains("//"))
            {
                normalizedPath = normalizedPath.Replace("//", "/");
            }

            return normalizedPath.TrimEnd('/');
        }

        public static bool IsAssetsPath(string assetPath)
        {
            string normalizedPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            return string.Equals(normalizedPath, "Assets", System.StringComparison.OrdinalIgnoreCase)
                   || normalizedPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeSystemPath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            return rawPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).Trim();
        }
    }
}
