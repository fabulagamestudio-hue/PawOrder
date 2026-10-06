using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Iung.Tools.Core.Lifecycle
{
    [MovedFrom(true, "ToolBox", null, "CustomDDL")]
    public class DontDestroyOnLoadObject : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(this);
        }
    }
}
