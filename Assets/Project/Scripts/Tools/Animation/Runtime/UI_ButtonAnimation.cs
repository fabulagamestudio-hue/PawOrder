using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Iung.Animation
{
    [RequireComponent(typeof(EventTrigger))]
    [RequireComponent(typeof(Button))]
    public class UI_ButtonAnimation : MonoBehaviour
    {
        public bool block;

        private Button targetButton;
        private RectTransform buttonRectTransform; // Reference to the button's RectTransform for animation
        private Vector3 originalScale; // To store the button's original scale
        private bool isCursorOverButton; // Tracks if the cursor is over the button
        private bool isClickAnimationPlaying = false; // Tracks if the click animation is playing

        EventTrigger eventTrigger;

        public ScrollRect _scroll;

        [SerializeField] UnityEvent HandleMouseEnter;
        [SerializeField] UnityEvent HandleMouseExit;

        public bool blockScroller;

        void Start()
        {
            targetButton = GetComponent<Button>();
            buttonRectTransform = GetComponent<RectTransform>();
            eventTrigger = GetComponent<EventTrigger>();

            if (targetButton == null || buttonRectTransform == null || eventTrigger == null)
            {
                Debug.LogWarning("[UI_ButtonAnimation] Missing required components.", this);
                enabled = false;
                return;
            }

            Navigation navigation = targetButton.navigation;
            navigation.mode = Navigation.Mode.None;
            targetButton.navigation = navigation;

            _scroll = GetComponentInParent<ScrollRect>();

            // Save the original scale of the button
            originalScale = buttonRectTransform.localScale;

            // Get the EventTrigger component, it will be added automatically if not present
            if (eventTrigger.triggers == null)
                eventTrigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
            else
                RemoveConfiguredEntries();


            Configure_Click();
            Configure_Entry();
            Configure_Exit();
            if (_scroll != null)
            {
                Configure_BeginDrag();
                Configure_Drag();
                Configure_EndDrag();
                Configure_InitializePotentialDrag();
                Configure_Scroll();
            }
        }

        void RemoveConfiguredEntries()
        {
            eventTrigger.triggers.RemoveAll(e =>
                e.eventID == EventTriggerType.PointerEnter ||
                e.eventID == EventTriggerType.PointerExit ||
                e.eventID == EventTriggerType.PointerClick ||
                e.eventID == EventTriggerType.BeginDrag ||
                e.eventID == EventTriggerType.Drag ||
                e.eventID == EventTriggerType.EndDrag ||
                e.eventID == EventTriggerType.InitializePotentialDrag ||
                e.eventID == EventTriggerType.Scroll);
        }


        #region Start Configuration

        public void Configure_Entry()
        {
            // Add the event for when the cursor enters the button (PointerEnter)
            EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            pointerEnterEntry.callback.AddListener((eventData) => { OnHoverEnter(); });
            eventTrigger.triggers.Add(pointerEnterEntry);
        }

        public void Configure_Click()
        {
            // Add the click event (PointerClick)
            EventTrigger.Entry clickEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerClick
            };
            clickEntry.callback.AddListener((eventData) => { PlayImpactAnimation(); });
            eventTrigger.triggers.Add(clickEntry);
        }

        public void Configure_Exit()
        {
            // Add the event for when the cursor exits the button (PointerExit)
            EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };
            pointerExitEntry.callback.AddListener((eventData) => { OnHoverExit(); });
            eventTrigger.triggers.Add(pointerExitEntry);
        }

        public void Configure_BeginDrag()
        {
            EventTrigger.Entry pointer = new EventTrigger.Entry
            {
                eventID = EventTriggerType.BeginDrag
            };
            pointer.callback.AddListener((data) => { _scroll.OnBeginDrag((PointerEventData)data); });
            eventTrigger.triggers.Add(pointer);
        }

        public void Configure_Drag()
        {
            EventTrigger.Entry pointer = new EventTrigger.Entry
            {
                eventID = EventTriggerType.Drag
            };
            pointer.callback.AddListener((data) => { _scroll.OnDrag((PointerEventData)data); });
            eventTrigger.triggers.Add(pointer);
        }

        public void Configure_EndDrag()
        {
            EventTrigger.Entry pointer = new EventTrigger.Entry
            {
                eventID = EventTriggerType.EndDrag
            };
            pointer.callback.AddListener((data) => { _scroll.OnEndDrag((PointerEventData)data); });
            eventTrigger.triggers.Add(pointer);
        }

        public void Configure_InitializePotentialDrag()
        {
            EventTrigger.Entry pointer = new EventTrigger.Entry
            {
                eventID = EventTriggerType.InitializePotentialDrag
            };
            pointer.callback.AddListener((data) => { _scroll.OnInitializePotentialDrag((PointerEventData)data); });
            eventTrigger.triggers.Add(pointer);
        }

        public void Configure_Scroll()
        {
            EventTrigger.Entry pointer = new EventTrigger.Entry
            {
                eventID = EventTriggerType.Scroll
            };
            pointer.callback.AddListener((data) => { _scroll.OnScroll((PointerEventData)data); });
            eventTrigger.triggers.Add(pointer);
        }


        #endregion





        // Impact animation when the button is clicked
        void PlayImpactAnimation()
        {
            if (block) return;

            // Check if the button is interactable and if a click animation is not already playing
            if (isCursorOverButton && !isClickAnimationPlaying)
            {
                isClickAnimationPlaying = true; // Set flag that click animation is playing
                buttonRectTransform.DOShakeScale(0.5f, 0.3f, 10, 90, true)
                    .OnComplete(() =>
                    {
                        CheckCursorPosition();
                        isClickAnimationPlaying = false; // Reset flag when animation is complete
                    });

            }
        }

        // Animation when hovering over the button
        void OnHoverEnter()
        {
            if (block) return;

            if(targetButton.interactable)
                isCursorOverButton = true; // Update the flag to indicate the cursor is over the button

            // Check if the button is interactable before executing hover animation
            if (targetButton.interactable && !isClickAnimationPlaying)
            {
                buttonRectTransform.DOScale(originalScale * 1.1f, 0.2f); // Increases button size
                HandleMouseEnter?.Invoke();

            }
        }

        // Animation when the cursor leaves the button
        void OnHoverExit()
        {
            if (block) return;

            // Check if the button is interactable and that no click animation is playing
            isCursorOverButton = false; // Update the flag to indicate the cursor is no longer over the button
            if (!isClickAnimationPlaying)
            {
                buttonRectTransform.DOScale(originalScale, 0.2f); // Restores original size

                HandleMouseExit?.Invoke();

            }
        }

        // Check if the cursor is still over the button after clicking
        void CheckCursorPosition()
        {
            if (block) return;

            // Check if the cursor is still over the button using RectTransformUtility
            if (!isCursorOverButton)
            {
                buttonRectTransform.DOScale(originalScale, 0.2f); // Restores original size
            }
            else if(isCursorOverButton && !targetButton.interactable)
            {
                buttonRectTransform.DOScale(originalScale, 0.2f); // Restores original size
                isCursorOverButton = false;
            }
            else
            {
                isCursorOverButton = true; // Keep the button enlarged
            }
        }

    }
}
