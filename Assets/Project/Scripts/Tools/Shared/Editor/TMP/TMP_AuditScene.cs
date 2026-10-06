#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using System.Linq;

namespace Iung.Tools.Shared.Editor.TMP
{
    // AI GUIDANCE:
    // - When to use: during scene audits to identify TMP_Text components using dynamic atlases, autosize, or ContentSizeFitter patterns.
    // - Prefer: running this tool before suggesting TMP performance or typography adjustments so diagnostics are based on scene data.
    // - Avoid: guessing TMP issue sources without running the audit when a structured report is available.
    // - Reason: menu path "Tools/TMP/Audit Active Scene" provides a repeatable scene-level diagnostic entry point.
    public static class TMP_AuditScene
    {
        [MenuItem("Tools/TMP/Audit Active Scene")]
        public static void AuditActiveScene()
        {
            var texts = Object.FindObjectsOfType<TMP_Text>(true);
            if (texts.Length == 0)
            {
                Debug.LogWarning("[TMP Audit] Nenhum TMP_Text na cena.");
                return;
            }

            int dyn = 0, autosize = 0, fitter = 0;

            foreach (var t in texts.OrderBy(x => x.name))
            {
                var f = t.font;
                var mode = f ? f.atlasPopulationMode : AtlasPopulationMode.Static;
                bool isDyn = f && mode == AtlasPopulationMode.Dynamic;
                bool hasAuto = t.enableAutoSizing;
                bool hasFitter =
                    t.GetComponent<UnityEngine.UI.ContentSizeFitter>() ||
                    t.GetComponentsInParent<UnityEngine.UI.ContentSizeFitter>(true).Any();

                if (isDyn) dyn++;
                if (hasAuto) autosize++;
                if (hasFitter) fitter++;

                if (isDyn || hasAuto || hasFitter)
                {
                    Debug.Log(
                        $"[TMP Audit] {t.name} | Font={(f ? f.name : "(none)")} | Mode={mode} | AutoSize={hasAuto} | FitterHere/Parent={hasFitter}",
                        t
                    );
                }
            }

            var fbList = TMP_Settings.fallbackFontAssets;
            var fbCount = fbList != null ? fbList.Count : 0;

            Debug.Log($"[TMP Audit] Textos: {texts.Length} | Dynamic: {dyn} | AutoSize: {autosize} | Com ContentSizeFitter (aqui ou pai): {fitter}");
            Debug.Log($"[TMP Audit] TMP Settings → Fallback Font Assets: {fbCount}");
        }
    }
}
#endif
