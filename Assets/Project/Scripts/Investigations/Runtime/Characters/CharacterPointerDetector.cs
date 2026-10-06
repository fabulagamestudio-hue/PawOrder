using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Fabula.PawOrder
{
    public sealed class CharacterPointerDetector : MonoBehaviour
    {
        #region Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Scene actor whose collider will be checked by the pointer raycast.")]
        private CharacterSceneActor characterSceneActor;

        [SerializeField]
        [Tooltip("Interaction controller that receives click requests after this detector validates the pointer target.")]
        private CharacterInteractionController characterInteractionController;

        [SerializeField]
        [Tooltip("Camera used to convert the screen pointer position into a 2D world position.")]
        private Camera interactionCamera;

        [Header("Detection")]
        [SerializeField]
        [Tooltip("Layers considered by the 2D pointer raycast.")]
        private LayerMask interactionLayerMask = Physics2D.DefaultRaycastLayers;

        [SerializeField]
        [Tooltip("Mouse button used to request character interaction. 0 = Left, 1 = Right, 2 = Middle.")]
        private int interactionMouseButton = 0;

        private readonly List<RaycastResult> uiRaycastResults = new();

        #endregion

        #region Unity Messages

        private void Update()
        {
            if (!CanDetectPointer())
            {
                ClearHoverState();
                return;
            }

            if (IsPointerOverUserInterface())
            {
                ClearHoverState();
                return;
            }

            bool isPointerOverCharacter = IsPointerOverCharacter();
            characterSceneActor.SetHoverState(isPointerOverCharacter);

            if (isPointerOverCharacter && IsInteractionButtonPressed())
            {
                characterInteractionController.TryRequestInteraction();
            }
        }

        private void OnDisable()
        {
            ClearHoverState();
        }

        #endregion

        #region Public API

#if UNITY_EDITOR
        public void ConfigureForEditor(
            CharacterSceneActor newCharacterSceneActor,
            CharacterInteractionController newCharacterInteractionController,
            Camera newInteractionCamera)
        {
            characterSceneActor = newCharacterSceneActor;
            characterInteractionController = newCharacterInteractionController;
            interactionCamera = newInteractionCamera;
        }
#endif

        #endregion

        #region Internal Logic

        private bool CanDetectPointer()
        {
            return characterSceneActor != null
                && characterSceneActor.CharacterData != null
                && characterSceneActor.InteractionCollider != null
                && characterSceneActor.InteractionCollider.enabled
                && characterInteractionController != null
                && interactionCamera != null
                && Mouse.current != null;
        }

        private bool IsPointerOverUserInterface()
        {
            EventSystem currentEventSystem = EventSystem.current;

            if (currentEventSystem == null)
            {
                return false;
            }

            Vector2 pointerScreenPosition = Mouse.current.position.ReadValue();
            PointerEventData pointerEventData = new PointerEventData(currentEventSystem)
            {
                position = pointerScreenPosition
            };

            uiRaycastResults.Clear();
            currentEventSystem.RaycastAll(pointerEventData, uiRaycastResults);
            return uiRaycastResults.Count > 0;
        }

        private bool IsPointerOverCharacter()
        {
            Vector2 pointerScreenPosition = Mouse.current.position.ReadValue();
            Vector3 pointerScreenPoint = new Vector3(pointerScreenPosition.x, pointerScreenPosition.y, GetPointerDepth());

            Vector3 pointerWorldPosition = interactionCamera.ScreenToWorldPoint(pointerScreenPoint);
            Vector2 pointerWorldPoint = new Vector2(pointerWorldPosition.x, pointerWorldPosition.y);

            RaycastHit2D hit = Physics2D.Raycast(pointerWorldPoint, Vector2.zero, 0f, interactionLayerMask);
            return hit.collider == characterSceneActor.InteractionCollider;
        }

        private float GetPointerDepth()
        {
            return Mathf.Abs(interactionCamera.transform.position.z - characterSceneActor.transform.position.z);
        }

        private bool IsInteractionButtonPressed()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return false;
            }

            switch (interactionMouseButton)
            {
                case 1:
                    return mouse.rightButton.wasPressedThisFrame;
                case 2:
                    return mouse.middleButton.wasPressedThisFrame;
                default:
                    return mouse.leftButton.wasPressedThisFrame;
            }
        }

        private void ClearHoverState()
        {
            if (characterSceneActor == null)
            {
                return;
            }

            characterSceneActor.SetHoverState(false);
        }

        #endregion
    }
}
