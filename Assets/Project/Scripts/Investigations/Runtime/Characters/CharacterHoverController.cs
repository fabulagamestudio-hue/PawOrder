using DG.Tweening;
using UnityEngine;

namespace Fabula.PawOrder
{
    public sealed class CharacterHoverController : MonoBehaviour
    {
        #region Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Scene actor controlled by this hover behaviour.")]
        private CharacterSceneActor characterSceneActor;

        [Header("Particle System")]
        [SerializeField]
        [Tooltip("Determines whether emission is disabled when this character is not hovered. Existing particles remain alive until their lifetime ends.")]
        private bool stopEmissionOnExit = true;

        [Header("Visual Feedback")]
        [SerializeField]
        [Tooltip("Scale multiplier applied while the cursor is hovering this character.")]
        private float hoverScaleMultiplier = 1.04f;

        [SerializeField]
        [Tooltip("Time used by the hover scale animation.")]
        private float hoverAnimationDuration = 0.18f;

        [SerializeField]
        [Tooltip("Ease used by the hover scale animation.")]
        private Ease hoverEase = Ease.OutQuad;

        private Vector3 cachedDefaultScale;
        private Tween scaleTween;

        #endregion

        #region Properties

        public CharacterSceneActor CharacterSceneActor => characterSceneActor;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            cachedDefaultScale = transform.localScale;
            DisableHoverParticleEmission();
        }

        private void OnEnable()
        {
            if (characterSceneActor == null)
            {
                return;
            }

            characterSceneActor.HoverStarted += HandleHoverStarted;
            characterSceneActor.HoverEnded += HandleHoverEnded;
        }

        private void OnDisable()
        {
            if (characterSceneActor != null)
            {
                characterSceneActor.HoverStarted -= HandleHoverStarted;
                characterSceneActor.HoverEnded -= HandleHoverEnded;
                characterSceneActor.SetHoverState(false);
            }

            KillScaleTween();
            DisableHoverParticleEmission();
        }

        private void OnDestroy()
        {
            KillScaleTween();
        }

        #endregion

        #region Public API

#if UNITY_EDITOR
        public void ConfigureForEditor(CharacterSceneActor newCharacterSceneActor)
        {
            characterSceneActor = newCharacterSceneActor;
        }
#endif

        #endregion

        #region Internal Logic

        private void HandleHoverStarted(CharacterSceneActor hoveredCharacterSceneActor)
        {
            if (!CanHandleHover(hoveredCharacterSceneActor))
            {
                return;
            }

            EnableHoverParticleEmission();
            PlayHoverScale(true);
        }

        private void HandleHoverEnded(CharacterSceneActor hoveredCharacterSceneActor)
        {
            if (hoveredCharacterSceneActor != characterSceneActor)
            {
                return;
            }

            DisableHoverParticleEmission();
            PlayHoverScale(false);
        }

        private bool CanHandleHover(CharacterSceneActor hoveredCharacterSceneActor)
        {
            return hoveredCharacterSceneActor == characterSceneActor
                && characterSceneActor.CharacterData != null
                && characterSceneActor.InteractionCollider != null
                && characterSceneActor.InteractionCollider.enabled;
        }

        private void EnableHoverParticleEmission()
        {
            ParticleSystem hoverParticleSystem = characterSceneActor.HoverParticleSystem;

            if (hoverParticleSystem == null)
            {
                return;
            }

            if (!hoverParticleSystem.gameObject.activeSelf)
            {
                hoverParticleSystem.gameObject.SetActive(true);
            }

            ParticleSystem.EmissionModule emissionModule = hoverParticleSystem.emission;
            emissionModule.enabled = true;

            if (!hoverParticleSystem.isPlaying)
            {
                hoverParticleSystem.Play(true);
            }
        }

        private void DisableHoverParticleEmission()
        {
            if (characterSceneActor == null || characterSceneActor.HoverParticleSystem == null)
            {
                return;
            }

            if (!stopEmissionOnExit)
            {
                return;
            }

            ParticleSystem hoverParticleSystem = characterSceneActor.HoverParticleSystem;
            ParticleSystem.EmissionModule emissionModule = hoverParticleSystem.emission;
            emissionModule.enabled = false;
        }

        #endregion

        #region DoTween

        private void PlayHoverScale(bool shouldHover)
        {
            KillScaleTween();

            Vector3 targetScale = shouldHover
                ? cachedDefaultScale * hoverScaleMultiplier
                : cachedDefaultScale;

            scaleTween = transform
                .DOScale(targetScale, hoverAnimationDuration)
                .SetEase(hoverEase)
                .SetLink(gameObject);
        }

        private void KillScaleTween()
        {
            if (scaleTween == null)
            {
                return;
            }

            scaleTween.Kill();
            scaleTween = null;
        }

        #endregion
    }
}
