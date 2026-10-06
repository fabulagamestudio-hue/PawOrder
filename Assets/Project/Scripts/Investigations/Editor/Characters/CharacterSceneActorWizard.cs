using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    public sealed class CharacterSceneActorWizard : EditorWindow
    {
        #region Fields

        private const string WindowTitle = "Character Scene Actor Wizard";
        private const string DefaultCharacterObjectName = "Character Scene Actor";
        private const string HoverParticleObjectName = "Hover Particle System";

        [SerializeField] private CharacterData characterData;
        [SerializeField] private List<CharacterInteractionData> interactionData = new();
        [SerializeField] private Sprite characterSprite;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private Vector2 colliderSize = Vector2.one;
        [SerializeField] private Vector2 colliderOffset = Vector2.zero;
        [SerializeField] private bool createAnimator;

        private Vector2 scrollPosition;

        #endregion

        #region Unity Messages

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawDataSection();
            DrawVisualSection();
            DrawColliderSection();
            DrawCreationSection();

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Public API

        [MenuItem("Fabula/Paw Order/Investigations/Create Character Scene Actor")]
        public static void Open()
        {
            CharacterSceneActorWizard window = GetWindow<CharacterSceneActorWizard>(WindowTitle);
            window.minSize = new Vector2(420f, 440f);
            window.Show();
        }

        #endregion

        #region Internal Logic

        private void DrawDataSection()
        {
            EditorGUILayout.LabelField("Data", EditorStyles.boldLabel);

            characterData = (CharacterData)EditorGUILayout.ObjectField(
                "Character Data",
                characterData,
                typeof(CharacterData),
                false);

            int newInteractionCount = Mathf.Max(
                0,
                EditorGUILayout.IntField("Interaction Count", interactionData.Count));

            while (interactionData.Count < newInteractionCount)
            {
                interactionData.Add(null);
            }

            while (interactionData.Count > newInteractionCount)
            {
                interactionData.RemoveAt(interactionData.Count - 1);
            }

            EditorGUI.indentLevel++;
            for (int index = 0; index < interactionData.Count; index++)
            {
                interactionData[index] = (CharacterInteractionData)EditorGUILayout.ObjectField(
                    $"Interaction {index + 1}",
                    interactionData[index],
                    typeof(CharacterInteractionData),
                    false);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8f);
        }

        private void DrawVisualSection()
        {
            EditorGUILayout.LabelField("Visual", EditorStyles.boldLabel);

            characterSprite = (Sprite)EditorGUILayout.ObjectField(
                "Character Sprite",
                characterSprite,
                typeof(Sprite),
                false);

            spriteMaterial = (Material)EditorGUILayout.ObjectField(
                "Sprite Material",
                spriteMaterial,
                typeof(Material),
                false);

            createAnimator = EditorGUILayout.Toggle("Create Animator", createAnimator);

            interactionCamera = (Camera)EditorGUILayout.ObjectField(
                "Interaction Camera",
                interactionCamera,
                typeof(Camera),
                true);

            using (new EditorGUI.DisabledScope(!createAnimator))
            {
                animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                    "Animator Controller",
                    animatorController,
                    typeof(RuntimeAnimatorController),
                    false);
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawColliderSection()
        {
            EditorGUILayout.LabelField("Collider", EditorStyles.boldLabel);

            colliderSize = EditorGUILayout.Vector2Field("Collider Size", colliderSize);
            colliderOffset = EditorGUILayout.Vector2Field("Collider Offset", colliderOffset);

            EditorGUILayout.Space(8f);
        }

        private void DrawCreationSection()
        {
            EditorGUILayout.LabelField("Creation", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(characterData == null))
            {
                if (GUILayout.Button("Create Character Scene Actor", GUILayout.Height(32f)))
                {
                    CreateCharacterSceneActor();
                }
            }

            if (characterData == null)
            {
                EditorGUILayout.HelpBox("Assign a Character Data asset before creating the scene actor.", MessageType.Info);
            }
        }

        private void CreateCharacterSceneActor()
        {
            GameObject rootObject = new GameObject(GetCharacterObjectName());
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Character Scene Actor");

            SpriteRenderer spriteRenderer = Undo.AddComponent<SpriteRenderer>(rootObject);
            spriteRenderer.sprite = characterSprite;

            if (spriteMaterial != null)
            {
                spriteRenderer.sharedMaterial = spriteMaterial;
            }

            if (createAnimator)
            {
                Animator animator = Undo.AddComponent<Animator>(rootObject);
                animator.runtimeAnimatorController = animatorController;
            }

            BoxCollider2D boxCollider = Undo.AddComponent<BoxCollider2D>(rootObject);
            boxCollider.size = colliderSize;
            boxCollider.offset = colliderOffset;

            GameObject particleObject = new GameObject(HoverParticleObjectName);
            Undo.RegisterCreatedObjectUndo(particleObject, "Create Hover Particle System");
            particleObject.transform.SetParent(rootObject.transform);
            particleObject.transform.localPosition = Vector3.zero;
            particleObject.transform.localRotation = Quaternion.identity;
            particleObject.transform.localScale = Vector3.one;

            ParticleSystem hoverParticleSystem = Undo.AddComponent<ParticleSystem>(particleObject);
            particleObject.SetActive(false);

            CharacterSceneActor characterSceneActor = Undo.AddComponent<CharacterSceneActor>(rootObject);
            CharacterHoverController characterHoverController = Undo.AddComponent<CharacterHoverController>(rootObject);
            CharacterInteractionController characterInteractionController = Undo.AddComponent<CharacterInteractionController>(rootObject);
            CharacterPointerDetector characterPointerDetector = Undo.AddComponent<CharacterPointerDetector>(rootObject);

            characterSceneActor.ConfigureForEditor(
                characterData,
                interactionData,
                spriteRenderer,
                hoverParticleSystem,
                boxCollider);

            characterHoverController.ConfigureForEditor(characterSceneActor);
            characterInteractionController.ConfigureForEditor(characterSceneActor);
            characterPointerDetector.ConfigureForEditor(characterSceneActor, characterInteractionController, interactionCamera);

            Selection.activeGameObject = rootObject;
            EditorGUIUtility.PingObject(rootObject);
        }

        private string GetCharacterObjectName()
        {
            if (characterData == null || string.IsNullOrWhiteSpace(characterData.DisplayName))
            {
                return DefaultCharacterObjectName;
            }

            return $"{characterData.DisplayName} Character Actor";
        }

        #endregion
    }
}
