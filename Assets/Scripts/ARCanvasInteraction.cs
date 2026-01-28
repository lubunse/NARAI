using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(Collider))]
public class ARCanvasInteraction : MonoBehaviour
{
    private static ARCanvasInteraction currentlySelectedCanvas; // Tracks which AR canvas is currently selected

    private ARRaycastManager arRaycastManager; // Used to detect AR planes
    private Camera arCamera;                   // Reference to the AR main camera
    private GameObject outlineFrame;           // Frame shown around the canvas when selected

    [Header("UI")]
    private Button deleteButton;               // UI button to delete the AR canvas
    [SerializeField] private RectTransform panelRect;     // The Panel area (whole canvas)
    [SerializeField] private RectTransform viewportRect;  // The Viewport area (swipe area)

    [Header("Scaling Limits")]
    [SerializeField] private float minScale = 0.05f;  // Minimum scale size for the canvas
    [SerializeField] private float maxScale = 0.2f;   // Maximum scale size for the canvas

    private bool isSelected = false;          // Whether this canvas is currently selected
    private Vector3 offset;                   // Offset between touch point and canvas position during drag

    private float initialPinchDistance = 0f;  // Distance between two fingers when pinch starts
    private Vector3 initialScale;             // Scale of the canvas before pinch starts

    private static List<ARRaycastHit> hits = new List<ARRaycastHit>(); // Temporary list for AR raycast results

    private bool isHolding = false;           // True if the user is holding the touch
    private bool canDrag = false;             // True if dragging is allowed

    private Plane dragPlane;                  // Virtual plane used to keep drag at a fixed depth
    private float dragDepth = 0f;             // Depth (distance along camera.forward) at which dragging happens


    private void Awake()
    {
        // Find the delete button in the UI hierarchy
        deleteButton = GameObject.Find("UICanvas/DeleteCanvasButton")?.GetComponent<Button>();

        // Get ARRaycastManager and AR camera
        arRaycastManager = FindFirstObjectByType<ARRaycastManager>();
        arCamera = Camera.main;

        // Find the "OutlineFrame" child object
        var frameTransform = transform.Find("OutlineFrame");
        if (frameTransform != null)
            outlineFrame = frameTransform.gameObject;

        // Hide the outline at start
        if (outlineFrame != null)
            outlineFrame.SetActive(false);
        else
            Debug.LogError("No Outline Frame found!");

        // Initialize delete button
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(false); // Hide at start
        }
        else
        {
            Debug.LogError("DeleteButton is not assigned!");
        }
    }

    private void Update()
    {
        if (Touchscreen.current == null)
            return;

        var touches = Touchscreen.current.touches;
        int activeTouches = touches.Count;

        // Reset interaction state when no touches are detected
        if (activeTouches == 0)
        {
            isHolding = false;
            canDrag = false;
            initialPinchDistance = 0f;
            return;
        }

        var touch0 = touches[0];
        Vector2 touchPos = touch0.position.ReadValue();
        var phase = touch0.phase.ReadValue();

        // Handle touch begin
        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            isHolding = false;
            canDrag = false;

            // Ignore touches on UI elements (like delete button)
            if (IsTouchOverUI(touchPos))
                return;

            Ray ray = arCamera.ScreenPointToRay(touchPos);

            // Check if the canvas was touched
            if (Physics.Raycast(ray, out var hit) && hit.transform == transform)
            {
                // Select this canvas
                SelectThisCanvas();

                // Dragging only allowed if the touch is inside the Panel BUT NOT inside the Viewport
                if (IsTouchInPanelButNotViewport(touchPos))
                {
                    // Calculate the drag depth (distance from camera to canvas along forward vector)
                    dragDepth = Vector3.Dot(transform.position - arCamera.transform.position, arCamera.transform.forward);

                    // Create a virtual drag plane
                    Vector3 planePoint = arCamera.transform.position + arCamera.transform.forward * dragDepth;
                    dragPlane = new Plane(arCamera.transform.forward, planePoint);

                    // Calculate offset between touch point and canvas position
                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 planeHitPoint = ray.GetPoint(enter);
                        offset = transform.position - planeHitPoint;
                    }
                    else
                    {
                        Vector3 worldPoint = arCamera.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, dragDepth));
                        offset = transform.position - worldPoint;
                    }

                    isHolding = true;
                    canDrag = true; // Dragging enabled
                }
                else
                {
                    // Only selection (no dragging if inside Viewport)
                    isHolding = true;
                    canDrag = false;
                }
            }
            else
            {
                // Touched elsewhere ? deselect canvas
                DeselectIfSelected();
            }
        }

        // Dragging logic (only if allowed)
        if (phase == UnityEngine.InputSystem.TouchPhase.Moved && isSelected && canDrag)
        {
            Ray ray = arCamera.ScreenPointToRay(touchPos);

            // Recalculate drag plane
            Vector3 planePoint = arCamera.transform.position + arCamera.transform.forward * dragDepth;
            dragPlane = new Plane(arCamera.transform.forward, planePoint);

            Vector3 worldPoint;
            if (dragPlane.Raycast(ray, out float enter))
            {
                worldPoint = ray.GetPoint(enter);
            }
            else
            {
                worldPoint = arCamera.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, dragDepth));
            }

            // Move canvas to new position
            Vector3 target = worldPoint + offset;
            transform.position = target;

            // Keep canvas facing the camera
            Vector3 lookDirection = transform.position - arCamera.transform.position;
            if (lookDirection != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        // Handle pinch-to-zoom scaling (works everywhere, even in Viewport)
        if (isSelected && activeTouches >= 2)
        {
            var t1 = touches[0];
            var t2 = touches[1];

            if (t1.isInProgress && t2.isInProgress)
            {
                Vector2 p1 = t1.position.ReadValue();
                Vector2 p2 = t2.position.ReadValue();

                float currentDistance = Vector2.Distance(p1, p2);

                if (initialPinchDistance == 0f)
                {
                    // Store initial distance and scale
                    initialPinchDistance = currentDistance;
                    initialScale = transform.localScale;
                }
                else
                {
                    // Calculate scaling factor
                    float scaleFactor = currentDistance / initialPinchDistance;
                    Vector3 newScale = initialScale * scaleFactor;

                    // Clamp scale to min/max
                    float clamped = Mathf.Clamp(newScale.x, minScale, maxScale);
                    transform.localScale = new Vector3(clamped, clamped, clamped);
                }
            }
        }
        else
        {
            // Reset pinch state if less than 2 touches
            if (activeTouches < 2)
                initialPinchDistance = 0f;
        }

        // If not selected, always rotate to face the camera
        if (!isSelected)
        {
            Vector3 cameraPosition = arCamera.transform.position;
            Vector3 lookDirection = transform.position - cameraPosition;
            if (lookDirection != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    private void SelectThisCanvas()
    {
        // Play sound when selecting new canvas
        if (currentlySelectedCanvas != this && SoundManager.Instance != null)
            SoundManager.Instance.PlayCanvasSelect();

        // Deselect other canvas if one is already selected
        if (currentlySelectedCanvas != null && currentlySelectedCanvas != this)
            currentlySelectedCanvas.Deselect();

        // Mark this one as selected
        isSelected = true;
        currentlySelectedCanvas = this;
        Highlight(true);

        // Attach delete button functionality
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(DeleteThisCanvas);
            deleteButton.gameObject.SetActive(true);
        }
    }

    private void DeselectIfSelected()
    {
        if (currentlySelectedCanvas == this)
        {
            Deselect();
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayCanvasSelect();
        }
    }

    private void Deselect()
    {
        // Clear selection
        isSelected = false;
        Highlight(false);
        currentlySelectedCanvas = null;
        isHolding = false;
        canDrag = false;

        // Hide delete button
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(false);
    }

    private void Highlight(bool active)
    {
        // Show/hide outline frame
        if (outlineFrame != null)
            outlineFrame.SetActive(active);

        // Show/hide delete button
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(active);
    }

    private void DeleteThisCanvas()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonClick();

        Debug.Log("Deleting canvas: " + gameObject.name);

        // Reset global reference if this is currently selected
        if (currentlySelectedCanvas == this)
            currentlySelectedCanvas = null;

        if (deleteButton != null)
            deleteButton.gameObject.SetActive(false);

        // Destroy parent ARAnchor (removes canvas from AR space)
        var anchor = GetComponentInParent<ARAnchor>();
        if (anchor != null)
        {
            Destroy(anchor.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private bool IsTouchOverUI(Vector2 touchPos)
    {
        // Raycast UI elements under the touch position
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = touchPos
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        // Detect if the touch is on a UI button (e.g. Delete button)
        foreach (var result in results)
        {
            var graphic = result.gameObject.GetComponent<Graphic>();
            if (graphic != null && graphic.raycastTarget && result.gameObject.GetComponent<Button>() != null)
                return true;
        }

        return false;
    }

    private bool IsTouchInPanelButNotViewport(Vector2 touchPos)
    {
        if (panelRect == null || viewportRect == null)
            return false;

        // Check if touch is inside the panel
        bool inPanel = RectTransformUtility.RectangleContainsScreenPoint(panelRect, touchPos, arCamera);
        if (!inPanel)
            return false;

        // Check if touch is also inside the viewport
        bool inViewport = RectTransformUtility.RectangleContainsScreenPoint(viewportRect, touchPos, arCamera);

        // Drag allowed if inside panel but outside viewport
        return inPanel && !inViewport;
    }

    public bool IsSelected()
    {
        return isSelected;
    }
}
