using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Iung.Tools.Shared.UI
{

    // AI GUIDANCE:
    // - When to use: when filling TMP text from lines or checking pointer-over-UI before input-driven gameplay actions.
    // - Prefer: reusing UIUtils methods for common UI interactions and avoiding duplicated TextMeshPro or EventSystem boilerplate.
    // - Avoid: replicating IsPointerOverUI checks in multiple scripts unless a special case requires custom behavior.
    // - Reason: SetTextFromLines already guards null targets and empty line collections.
    public static class UIUtils
    {
        public static void SetTextFromLines(this TextMeshProUGUI text, List<string> lines)
        {
            if (text == null || lines == null || lines.Count == 0)
                return;

            text.text = string.Join("\n", lines);
        }

        /// <summary>
        /// Returns true if the current pointer is over a UI element.
        /// </summary>
        public static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }

}
