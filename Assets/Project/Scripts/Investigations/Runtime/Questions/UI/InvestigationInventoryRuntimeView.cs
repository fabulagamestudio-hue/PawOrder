using System;
using System.Collections.Generic;
using Iung.Animation;
using UnityEngine;

namespace Fabula.PawOrder
{
    public sealed class InvestigationInventoryRuntimeView : MonoBehaviour
    {
        #region Fields

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Root transform where collected evidence items are instantiated.")]
        private RectTransform itemsRoot;

        [SerializeField]
        [Tooltip("Prefab used to instantiate each collected evidence item.")]
        private InvestigationInventoryItemView itemTemplate;

        [Header("Manual Layout")]
        [SerializeField]
        [Tooltip("Fixed size applied to each instantiated evidence item.")]
        private Vector2 itemSize = new Vector2(96f, 96f);

        [SerializeField]
        [Tooltip("Spacing applied between evidence items.")]
        private Vector2 itemSpacing = new Vector2(12f, 12f);

        [SerializeField]
        [Tooltip("Maximum number of evidence items per row before wrapping to the next line.")]
        private int maxItemsPerRow = 3;

        [Header("AnimationProperty")]
        [SerializeField]
        [Tooltip("AnimationProperty entries used to show and hide the inventory interface.")]
        private List<AnimationProperty> interfaceAnimations = new();

        private readonly List<InvestigationInventoryItemView> spawnedItems = new();

        #endregion

        #region Events

        public event Action<ItemPromptData> ItemSelected;

        #endregion

        #region Unity Messages

        private void Awake()
        {
            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.ConfigureStartPosition();
            }
        }

        private void OnDestroy()
        {
            ClearItems();
        }

        #endregion

        #region Public API

        public void ShowItems(IReadOnlyList<ItemPromptData> items)
        {
            ClearItems();

            if (itemsRoot == null || itemTemplate == null || items == null)
            {
                return;
            }

            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                SpawnItem(items[itemIndex], itemIndex);
            }

            ApplyContentSize(items.Count);

            if (interfaceAnimations.Count > 0)
            {
                interfaceAnimations.RevealThis();
            }
        }

        public void Hide()
        {
            if (interfaceAnimations.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            interfaceAnimations.HideThis(() => gameObject.SetActive(false));
        }

        #endregion

        #region Internal Logic

        private void SpawnItem(ItemPromptData itemPrompt, int itemIndex)
        {
            if (itemPrompt == null)
            {
                return;
            }

            InvestigationInventoryItemView itemView = Instantiate(itemTemplate, itemsRoot);
            itemView.gameObject.SetActive(true);
            itemView.Bind(itemPrompt, () => ItemSelected?.Invoke(itemPrompt));

            RectTransform itemRectTransform = itemView.RectTransform;
            if (itemRectTransform != null)
            {
                itemRectTransform.anchorMin = new Vector2(0f, 1f);
                itemRectTransform.anchorMax = new Vector2(0f, 1f);
                itemRectTransform.pivot = new Vector2(0f, 1f);
                itemRectTransform.sizeDelta = itemSize;
                itemRectTransform.anchoredPosition = GetItemPosition(itemIndex);
            }

            itemView.Show();
            spawnedItems.Add(itemView);
        }

        private Vector2 GetItemPosition(int itemIndex)
        {
            int safeMaxItemsPerRow = Mathf.Max(1, maxItemsPerRow);
            int columnIndex = itemIndex % safeMaxItemsPerRow;
            int rowIndex = itemIndex / safeMaxItemsPerRow;

            float xPosition = columnIndex * (itemSize.x + itemSpacing.x);
            float yPosition = -rowIndex * (itemSize.y + itemSpacing.y);
            return new Vector2(xPosition, yPosition);
        }

        private void ApplyContentSize(int itemCount)
        {
            if (itemsRoot == null)
            {
                return;
            }

            int safeMaxItemsPerRow = Mathf.Max(1, maxItemsPerRow);
            int rowCount = Mathf.CeilToInt(itemCount / (float)safeMaxItemsPerRow);
            float width = safeMaxItemsPerRow * itemSize.x + Mathf.Max(0, safeMaxItemsPerRow - 1) * itemSpacing.x;
            float height = rowCount * itemSize.y + Mathf.Max(0, rowCount - 1) * itemSpacing.y;
            itemsRoot.sizeDelta = new Vector2(width, height);
        }

        private void ClearItems()
        {
            foreach (InvestigationInventoryItemView itemView in spawnedItems)
            {
                if (itemView != null)
                {
                    Destroy(itemView.gameObject);
                }
            }

            spawnedItems.Clear();
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

        public void ConfigureForEditor(
            RectTransform newItemsRoot,
            InvestigationInventoryItemView newItemTemplate,
            Vector2 newItemSize,
            Vector2 newItemSpacing,
            int newMaxItemsPerRow)
        {
            itemsRoot = newItemsRoot;
            itemTemplate = newItemTemplate;
            itemSize = newItemSize;
            itemSpacing = newItemSpacing;
            maxItemsPerRow = newMaxItemsPerRow;
        }

        #endregion
#endif
    }
}
