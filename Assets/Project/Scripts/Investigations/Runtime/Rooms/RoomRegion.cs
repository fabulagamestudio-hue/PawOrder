using UnityEngine;

namespace Fabula.PawOrder
{
    public sealed class RoomRegion : MonoBehaviour
    {
        #region Fields

        [Header("Room Data")]
        [SerializeField]
        [Tooltip("Investigation location represented by this room region.")]
        private LocationData locationData;

        [Header("Camera")]
        [SerializeField]
        [Tooltip("Transform that defines where the camera should move when this room becomes active.")]
        private Transform cameraTarget;

        #endregion

        #region Properties

        public LocationData LocationData => locationData;
        public Transform CameraTarget => cameraTarget;

        #endregion

        #region Public API

        public bool HasValidCameraTarget()
        {
            return cameraTarget != null;
        }

        #endregion
    }
}
