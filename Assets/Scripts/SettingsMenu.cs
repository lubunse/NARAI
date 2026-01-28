using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [Header("References")]
    public GameObject settingsCanvas;   // Root canvas of the settings menu
    public Button settingsButton;       // Top-right button to open/close settings
    public Slider volumeSlider;         // Slider to adjust volume
    public TMP_Dropdown themeDropdown;  // Dropdown to switch themes
    public TMP_Text volumeLabel;        // Text label to show volume percentage
    public Button scanButton;           // Scan-Button

    [Header("Onboarding")]
    public GameObject onboardingCanvas; // Separate onboarding canvas with instructions
    public Button onboardingButton;     // Button inside settings to open onboarding

    [Header("Behavior")]
    [Range(0f, 1f)]
    public float initialVolume = 1f;          // Default volume on start
    public float toggleIgnoreDuration = 0.25f; // Delay after toggle to avoid instant close

    private float lastToggleTime = -10f;      // Last time settings/onboarding was toggled

    private GraphicRaycaster settingsRaycaster;
    private GraphicRaycaster onboardingRaycaster;
    private EventSystem eventSystem;

    private void Start()
    {
        if (settingsCanvas == null)
        {
            Debug.LogError("SettingsMenu: settingsCanvas not assigned.");
            return;
        }

        // Ensure raycaster for settings canvas
        settingsRaycaster = settingsCanvas.GetComponent<GraphicRaycaster>();
        if (settingsRaycaster == null)
            settingsRaycaster = settingsCanvas.AddComponent<GraphicRaycaster>();

        // Ensure raycaster for onboarding canvas
        if (onboardingCanvas != null)
        {
            onboardingRaycaster = onboardingCanvas.GetComponent<GraphicRaycaster>();
            if (onboardingRaycaster == null)
                onboardingRaycaster = onboardingCanvas.AddComponent<GraphicRaycaster>();
            onboardingCanvas.SetActive(false);
        }

        // Find EventSystem
        eventSystem = EventSystem.current;
        if (eventSystem == null)
            Debug.LogError("SettingsMenu: No EventSystem found in scene.");

        // Hide settings by default
        settingsCanvas.SetActive(false);

        // Setup slider
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            volumeSlider.value = Mathf.Clamp01(initialVolume);
            UpdateVolumeLabel(volumeSlider.value);

            // Add triggers for slider drag sound loop
            AddEventTrigger(volumeSlider.gameObject, EventTriggerType.PointerDown, (data) => StartSettingsLoop());
            AddEventTrigger(volumeSlider.gameObject, EventTriggerType.PointerUp, (data) => StopSettingsLoop());
            AddEventTrigger(volumeSlider.gameObject, EventTriggerType.EndDrag, (data) => StopSettingsLoop());
        }

        // Setup theme dropdown
        if (themeDropdown != null)
        {
            themeDropdown.onValueChanged.RemoveAllListeners();
            themeDropdown.onValueChanged.AddListener(OnThemeChanged);
        }

        // Setup onboarding button
        if (onboardingButton != null)
        {
            onboardingButton.onClick.RemoveAllListeners();
            onboardingButton.onClick.AddListener(OpenOnboarding);
        }

        // Apply initial volume
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetVolume(volumeSlider != null ? volumeSlider.value : initialVolume);

        // Prevent instant closing right after start
        lastToggleTime = Time.time;
    }

    private void Update()
    {
        // If settings is open ? check clicks outside
        if (settingsCanvas != null && settingsCanvas.activeSelf)
        {
            if (Time.time - lastToggleTime >= toggleIgnoreDuration)
            {
                if (CheckPointerDown(out Vector2 pointerPos))
                {
                    if (!IsPointerOverCanvasUI(settingsRaycaster, pointerPos))
                        CloseSettings(playClickSound: true);
                }
            }
        }

        // If onboarding is open ? check clicks outside
        if (onboardingCanvas != null && onboardingCanvas.activeSelf)
        {
            if (Time.time - lastToggleTime >= toggleIgnoreDuration)
            {
                if (CheckPointerDown(out Vector2 pointerPos))
                {
                    if (!IsPointerOverCanvasUI(onboardingRaycaster, pointerPos))
                        CloseOnboarding();
                }
            }
        }
    }

    // Toggle settings menu by button
    public void ToggleSettings()
    {
        if (settingsCanvas == null) return;

        bool active = settingsCanvas.activeSelf;
        if (active)
        {
            CloseSettings(playClickSound: true);
            lastToggleTime = Time.time;
            return;
        }

        OpenSettings();
        lastToggleTime = Time.time;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonClick();
    }

    private void OpenSettings()
    {
        // Disable Scan Button
        scanButton.interactable = false;

        if (settingsCanvas == null) return;
        settingsCanvas.SetActive(true);
        settingsButton.enabled = false;

        // Focus the slider for immediate interaction
        if (eventSystem != null && volumeSlider != null)
            eventSystem.SetSelectedGameObject(volumeSlider.gameObject);
    }

    public void CloseSettings(bool playClickSound = false)
    {
        if (settingsCanvas == null) return;

        settingsCanvas.SetActive(false);
        StopSettingsLoop();

        if (playClickSound && SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonClick();

        // Enable Scan Button
        scanButton.interactable = true;
        settingsButton.enabled = true;
    }

    // Called when volume slider changes
    private void OnVolumeChanged(float value)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetVolume(value);

        UpdateVolumeLabel(value);
    }

    // Called when theme is changed in dropdown
    private void OnThemeChanged(int index)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonClick();

        if (ColorThemeManager.Instance != null)
            ColorThemeManager.Instance.ApplyTheme(index);
    }

    // Update label to show percentage value
    private void UpdateVolumeLabel(float value)
    {
        int percent = Mathf.RoundToInt(value * 100f);
        if (volumeLabel != null)
            volumeLabel.text = percent + "%";
    }

    // Play looping sound while slider is dragged
    private void StartSettingsLoop()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySettingsLoop();
    }

    // Stop looping sound when slider released
    private void StopSettingsLoop()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.StopSettingsLoop();
    }

    // Open onboarding canvas and close settings
    private void OpenOnboarding()
    {
        if (onboardingCanvas == null) return;

        onboardingCanvas.SetActive(true);
        settingsCanvas.SetActive(false);
        lastToggleTime = Time.time;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonClick();
    }

    // Close onboarding canvas
    private void CloseOnboarding()
    {
        if (onboardingCanvas == null) return;

        onboardingCanvas.SetActive(false);
        settingsButton.enabled = true;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonClick();

        // Enable Scan Button
        scanButton.interactable = true;
    }

    // --- Input Utilities ---

    // Detect pointer/touch down and return position
    private bool CheckPointerDown(out Vector2 pointerPos)
    {
        pointerPos = Vector2.zero;

        // New Input System mouse
        if (UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            pointerPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            return true;
        }
        // New Input System touch
        else if (UnityEngine.InputSystem.Touchscreen.current != null &&
                 UnityEngine.InputSystem.Touchscreen.current.touches.Count > 0)
        {
            var t = UnityEngine.InputSystem.Touchscreen.current.touches[0];
            if (t.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                pointerPos = t.position.ReadValue();
                return true;
            }
        }
        // Old Input System mouse
        else if (Input.GetMouseButtonDown(0))
        {
            pointerPos = Input.mousePosition;
            return true;
        }
        // Old Input System touch
        else if (Input.touchCount > 0)
        {
            var tt = Input.GetTouch(0);
            if (tt.phase == TouchPhase.Began)
            {
                pointerPos = tt.position;
                return true;
            }
        }

        return false;
    }

    // Check if pointer is over a canvas UI element
    private bool IsPointerOverCanvasUI(GraphicRaycaster raycaster, Vector2 screenPosition)
    {
        if (eventSystem == null || raycaster == null) return false;

        PointerEventData ped = new PointerEventData(eventSystem) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(ped, results);

        if (results == null || results.Count == 0)
            return false;

        foreach (var r in results)
        {
            if (r.gameObject != null && r.gameObject.transform.IsChildOf(raycaster.transform))
                return true;
            if (r.gameObject == raycaster.gameObject)
                return true;
        }

        return false;
    }

    // Helper to add event triggers dynamically
    private void AddEventTrigger(GameObject go, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        if (go == null) return;
        EventTrigger et = go.GetComponent<EventTrigger>();
        if (et == null) et = go.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        et.triggers.Add(entry);
    }

    private void OnDisable()
    {
        // Stop slider loop sound if settings is disabled
        StopSettingsLoop();
    }
}
