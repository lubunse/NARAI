using UnityEngine;
using UnityEngine.UI;

public class ColorThemeManager : MonoBehaviour
{
    public static ColorThemeManager Instance;

    [Header("UI Elements")]
    public Button scanButton;       // Button for scanning
    public Button deleteButton;     // Button for deleting
    public Button settingsButton;   // Button for opening settings
    public Button onboardingButton; // Button for opening onboarding
    public Image[] canvasBackgrounds; // Array of background images for canvases

    [Header("Color Schemes")]
    public Color buttonColorSilver = new Color(192f / 255f, 192f / 255f, 192f / 255f);
    public Color bgColorSilver = new Color(192f / 255f, 192f / 255f, 192f / 255f);

    public Color buttonColorGreen = new Color(50f / 255f, 205f / 255f, 50f / 255f);
    public Color bgColorGreen = new Color(50f / 255f, 205f / 255f, 50f / 255f);

    public Color buttonColorCyan = new Color(0f, 1f, 1f);
    public Color bgColorCyan = new Color(0f, 1f, 1f);

    private void Awake()
    {
        // Singleton instance for global access
        Instance = this;

        // Apply default theme (Silver)
        ApplyTheme(0);
    }

    public void ApplyTheme(int themeIndex)
    {
        Color buttonColor = Color.white;
        Color bgColor = Color.white;

        // Select colors based on theme index
        switch (themeIndex)
        {
            case 0: // Silver (default)
                buttonColor = buttonColorSilver;
                bgColor = bgColorSilver;
                break;
            case 1: // Green
                buttonColor = buttonColorGreen;
                bgColor = bgColorGreen;
                break;
            case 2: // Cyan
                buttonColor = buttonColorCyan;
                bgColor = bgColorCyan;
                break;
        }

        // Apply button colors
        scanButton.image.color = buttonColor;
        deleteButton.image.color = buttonColor;
        settingsButton.image.color = buttonColor;
        onboardingButton.image.color = buttonColor;
    }
}
