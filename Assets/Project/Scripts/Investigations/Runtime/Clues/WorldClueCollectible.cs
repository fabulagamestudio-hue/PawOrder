using System;
using UnityEngine;

namespace Fabula.PawOrder
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WorldClueCollectible : MonoBehaviour
    {
        #region Fields

        [Header("Data")]
        [SerializeField]
        [Tooltip("Investigation item registered when this clue is collected.")]
        private ItemPromptData itemData;

        private bool isCollected;

        #endregion

        #region Properties

        public ItemPromptData ItemData => itemData;
        public bool IsCollected => isCollected;
        public float InspectRadius => itemData != null ? Mathf.Max(0f, itemData.InspectRadius) : 0f;

        #endregion

        #region Events

        public event Action<WorldClueCollectible, ItemPromptData> Collected;

        #endregion

        #region Public API

        public void Collect()
        {
            if (isCollected || itemData == null)
            {
                return;
            }

            isCollected = true;
            Collected?.Invoke(this, itemData);
            gameObject.SetActive(false);
        }

        public float GetDistanceFrom(Vector2 worldPosition)
        {
            Vector2 cluePosition = transform.position;
            return Vector2.Distance(worldPosition, cluePosition);
        }

        #endregion

        #region Gizmos/Debug

        private void OnDrawGizmosSelected()
        {
            if (itemData == null)
            {
                return;
            }

            Gizmos.DrawWireSphere(transform.position, InspectRadius);
        }

        #endregion
    }
}
