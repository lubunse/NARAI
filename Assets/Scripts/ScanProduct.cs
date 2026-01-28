using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

#if UNITY_IOS
using System.Runtime.InteropServices;
#endif

public class ScanProduct : MonoBehaviour
{
    public Button scanButton;                        // Button to trigger the scan
    [Header("Scan Button Cooldown")]
    public float scanButtonCooldown = 10f;          // Duration in seconds before button can be pressed again
    public Canvas AnalyzingCanvas;                  // Canvas shown during analysis
    public Canvas ErrorCanvas;                       // Canvas shown on error

    private void Start()
    {
        // Add listener to scan button to start image capture and analysis
        scanButton.onClick.AddListener(() => StartCoroutine(CaptureAndAnalyze()));
        Debug.Log("ScanProduct started. Click the button to capture and analyze an image.");

#if UNITY_ANDROID
        // Request write permission on Android if not already granted
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageWrite);
        }
#endif
    }

    private IEnumerator CaptureAndAnalyze()
    {
        // Play click sound
        SoundManager.Instance.PlayButtonClick();

        // Disable the scan button temporarily to prevent spamming
        //StartCoroutine(DisableScanButtonForSeconds(scanButtonCooldown));

        yield return new WaitForEndOfFrame(); // Wait until the frame is rendered

        // Capture screenshot
        Texture2D screenImage = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenImage.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenImage.Apply();

        // Resize image to max 1024px !
        //Texture2D resizedImage = ResizeTexture(screenImage, 1024);

        // Encode to JPG !
        //byte[] imageBytes = screenImage.EncodeToJPG(100);
        //string base64Image = Convert.ToBase64String(imageBytes);

        // Encode screenshot to PNG and convert to Base64
        byte[] imageBytes = screenImage.EncodeToPNG();
        string base64Image = Convert.ToBase64String(imageBytes);

        // Optionally save screenshot to gallery
        // SaveToGallery(imageBytes);

        // Store camera position and orientation at capture time
        Vector3 position = Camera.main.transform.position + Camera.main.transform.forward * 1.0f;
        Quaternion rotation = Quaternion.LookRotation(Camera.main.transform.forward);

        // Send image for analysis
        AnalyzeProduct.Instance.AnalyzeImage(base64Image, position, rotation);
    }
    private Texture2D ResizeTexture(Texture2D source, int maxSize)
    {
        // Behalte das Seitenverhältnis bei
        float scale = Mathf.Min((float)maxSize / source.width, (float)maxSize / source.height);
        int newWidth = Mathf.RoundToInt(source.width * scale);
        int newHeight = Mathf.RoundToInt(source.height * scale);

        RenderTexture rt = new RenderTexture(newWidth, newHeight, 24);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        rt.Release();

        return result;
    }


    private IEnumerator DisableScanButtonForSeconds(float seconds)
    {
        if (scanButton != null)
        {
            scanButton.interactable = false; // Disable button
            yield return new WaitForSeconds(seconds);
            scanButton.interactable = true;  // Re-enable button
        }
    }

    private void SaveToGallery(byte[] imageBytes)
    {
        string filename = $"AR_Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";

#if UNITY_ANDROID
        // Save file to persistent data path
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, imageBytes);

        // Copy to public gallery folder
        string galleryPath = Path.Combine("/sdcard/DCIM/ARApp", filename);
        Directory.CreateDirectory(Path.GetDirectoryName(galleryPath));
        File.Copy(path, galleryPath, true);

        // Trigger media scan to update gallery
        using (AndroidJavaClass mediaScanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
        using (AndroidJavaObject context = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                   .GetStatic<AndroidJavaObject>("currentActivity"))
        {
            mediaScanner.CallStatic("scanFile", context, new string[] { galleryPath }, null, null);
        }

        Debug.Log("Screenshot saved to gallery (Android): " + galleryPath);

#elif UNITY_IOS
        // Save file to persistent path and then to iOS gallery
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, imageBytes);
        SaveImageToGallery(path);
        Debug.Log("Screenshot saved to gallery (iOS): " + path);

#else
        // Save screenshot locally in editor
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, imageBytes);
        Debug.Log("Screenshot saved (Editor): " + path);
#endif
    }

#if UNITY_IOS
    [DllImport("__Internal")]
    private static extern void _SaveImageToGallery(string path); // Native iOS function to save image

    private void SaveImageToGallery(string path)
    {
        _SaveImageToGallery(path); // Call native function
    }
#endif
}
