using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fabula.PawOrder
{
    public sealed class CharacterSceneActor : MonoBehaviour
    {
        #region Fields

        [Header("Data")]
        [SerializeField]
        [Tooltip("Character identity used by investigation and dialogue systems.")]
        private CharacterData characterData;

        [SerializeField]
        [Tooltip("Available interactions for this scene character.")]
        private List<CharacterInteractionData> interactionData = new();

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Sprite renderer that visually represents this character.")]
        private SpriteRenderer spriteRenderer;

        [SerializeField]
        [Tooltip("Particle system toggled while the cursor is hovering this character.")]
        private ParticleSystem hoverParticleSystem;

        [SerializeField]
        [Tooltip("Collider used by explicit pointer raycasts for hover and click detection.")]
        private Collider2D interactionCollider;

        private bool isHovered;

        #endregion

        #region Properties

        public CharacterData CharacterData => characterData;
        public IReadOnlyList<CharacterInteractionData> InteractionData => interactionData;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public ParticleSystem HoverParticleSystem => hoverParticleSystem;
        public Collider2D InteractionCollider => interactionCollider;
        public bool IsHovered => isHovered;

        #endregion

        #region Events

        public event Action<CharacterSceneActor> HoverStarted;
        public event Action<CharacterSceneActor> HoverEnded;
        public event Action<CharacterSceneActor> InteractionRequested;

        #endregion

        #region Public API

        public void SetHoverState(bool newHoverState)
        {
            if (isHovered == newHoverState)
            {
                return;
            }

            isHovered = newHoverState;

            if (isHovered)
            {
                HoverStarted?.Invoke(this);
                return;
            }

            HoverEnded?.Invoke(this);
        }

        public void RequestInteraction()
        {
            if (characterData == null)
            {
                return;
            }

            InteractionRequested?.Invoke(this);
        }

        public bool HasValidSceneReferences()
        {
            return characterData != null
                && spriteRenderer != null
                && hoverParticleSystem != null
                && interactionCollider != null;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            CharacterData newCharacterData,
            IReadOnlyList<CharacterInteractionData> newInteractionData,
            SpriteRenderer newSpriteRenderer,
            ParticleSystem newHoverParticleSystem,
            Collider2D newInteractionCollider)
        {
            characterData = newCharacterData;
            interactionData = newInteractionData != null
                ? new List<CharacterInteractionData>(newInteractionData)
                : new List<CharacterInteractionData>();
            spriteRenderer = newSpriteRenderer;
            hoverParticleSystem = newHoverParticleSystem;
            interactionCollider = newInteractionCollider;
        }
#endif

        #endregion
    }
}
