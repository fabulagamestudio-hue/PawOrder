using System;
using UnityEngine;

namespace Fabula.PawOrder
{
    public sealed class CharacterInteractionController : MonoBehaviour
    {
        #region Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Scene actor that requests interaction and dialogue flow.")]
        private CharacterSceneActor characterSceneActor;

        [Header("Interaction")]
        [SerializeField]
        [Tooltip("Allows the player to click this character only while it is hovered.")]
        private bool requireHoverBeforeInteraction = true;

        #endregion

        #region Properties

        public CharacterSceneActor CharacterSceneActor => characterSceneActor;

        #endregion

        #region Events

        public event Action<CharacterSceneActor> CharacterSelected;

        #endregion

        #region Unity Messages

        private void OnEnable()
        {
            if (characterSceneActor == null)
            {
                return;
            }

            characterSceneActor.InteractionRequested += HandleInteractionRequested;
        }

        private void OnDisable()
        {
            if (characterSceneActor == null)
            {
                return;
            }

            characterSceneActor.InteractionRequested -= HandleInteractionRequested;
        }

        #endregion

        #region Public API

        public void TryRequestInteraction()
        {
            if (!CanRequestInteraction())
            {
                return;
            }

            characterSceneActor.RequestInteraction();
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(CharacterSceneActor newCharacterSceneActor)
        {
            characterSceneActor = newCharacterSceneActor;
        }
#endif

        #endregion

        #region Internal Logic

        private bool CanRequestInteraction()
        {
            if (characterSceneActor == null || characterSceneActor.CharacterData == null)
            {
                return false;
            }

            if (!requireHoverBeforeInteraction)
            {
                return true;
            }

            return characterSceneActor.IsHovered;
        }

        private void HandleInteractionRequested(CharacterSceneActor selectedCharacterSceneActor)
        {
            CharacterSelected?.Invoke(selectedCharacterSceneActor);
        }

        #endregion
    }
}
