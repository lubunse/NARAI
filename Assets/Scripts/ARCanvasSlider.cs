using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ARCanvasSlider : MonoBehaviour
{
    public RectTransform slideContainer;   // The container holding all slide panels
    public float swipeThreshold = 50f;     // Minimum horizontal movement (pixels) to trigger a slide change
    public float transitionSpeed = 10f;    // Speed of slide transition animation
    public Image[] indicators;             // UI dots to indicate the current slide

    private int currentSlide = 0;          // Index of the currently visible slide
    private Vector2 touchStartPos;         // Position where the touch started
    private bool isDragging = false;       // Whether the user is currently swiping

    private ARCanvasInteraction interaction;   // Reference to the ARCanvasInteraction (to check if canvas is selected)

    private float slideWidth;              // Width of a single slide

    private void Awake()
    {
        // Get the ARCanvasInteraction component attached to this GameObject
        interaction = GetComponent<ARCanvasInteraction>();
    }

    private IEnumerator Start()
    {
        // Wait one frame to ensure child slides are initialized
        yield return null;

        // If the container has at least one child, calculate the slide width
        if (slideContainer.childCount > 0)
        {
            RectTransform firstSlide = slideContainer.GetChild(0) as RectTransform;
            slideWidth = firstSlide.rect.width;
            Debug.Log("Slide width: " + slideWidth);
        }
    }

    private void Update()
    {
        // Stop if no touchscreen is present, or canvas not selected
        if (Touchscreen.current == null || interaction == null || !interaction.IsSelected())
            return;

        var touches = Touchscreen.current.touches;
        if (touches.Count == 0)
            return;

        var touch = touches[0];
        var phase = touch.phase.ReadValue();
        var pos = touch.position.ReadValue();

        // Touch begins
        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            touchStartPos = pos;
            isDragging = true;
        }
        // Touch ends
        else if (phase == UnityEngine.InputSystem.TouchPhase.Ended && isDragging)
        {
            float delta = pos.x - touchStartPos.x;

            // Check if swipe was strong enough
            if (Mathf.Abs(delta) > swipeThreshold)
            {
                // Swipe left ? next slide
                if (delta < 0 && currentSlide < 4)
                    currentSlide++;
                // Swipe right ? previous slide
                else if (delta > 0 && currentSlide > 0)
                    currentSlide--;
            }

            isDragging = false;
            UpdateIndicators();
        }

        // Smoothly move container to the target slide position
        Vector2 targetPos = new Vector2(-currentSlide * slideWidth, 0);
        slideContainer.anchoredPosition = Vector2.Lerp(slideContainer.anchoredPosition, targetPos, Time.deltaTime * transitionSpeed);
    }

    // Update the visual indicator dots for the current slide
    private void UpdateIndicators()
    {
        for (int i = 0; i < indicators.Length; i++)
        {
            indicators[i].color = (i == currentSlide) ? Color.white : Color.gray3; // Active = white, inactive = gray
            indicators[i].rectTransform.localScale = (i == currentSlide) ? Vector3.one * 1.5f : Vector3.one; // Enlarge active indicator
        }
    }
}
