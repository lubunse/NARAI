using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("UI Sounds")]
    public AudioClip buttonClick;       // Sound for button clicks
    public AudioClip canvasSelect;      // Sound when a canvas is selected
    public AudioClip analysisComplete;  // Sound when analysis is finished
    public AudioClip error;             // Sound for error events

    [Header("Settings loop sound (assign loopable clip)")]
    public AudioClip settingsLoopClip;  // Looping sound that plays while slider is pressed

    private AudioSource audioSource;    // Main audio source for one-shot sounds

    // Dedicated source for looping settings sound
    private AudioSource loopSource;

    private float volume = 1f; // Default volume set to 100%

    private void Awake()
    {
        // Singleton pattern: only one SoundManager instance should exist
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep SoundManager across scene loads

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // Prevent auto-play on start
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate SoundManager instances
        }
    }

    // Adjusts global volume between 0 and 1
    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        if (audioSource != null)
            audioSource.volume = volume;
        if (loopSource != null)
            loopSource.volume = volume;
    }

    // Public methods for playing predefined UI sounds
    public void PlayButtonClick() => PlayOneShot(buttonClick);
    public void PlayCanvasSelect() => PlayOneShot(canvasSelect);
    public void PlayAnalysisComplete() => PlayOneShot(analysisComplete);
    public void PlayError() => PlayOneShot(error);

    // Helper method to play one-shot sounds
    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }

    // Start the continuous settings loop (only if a loop clip is assigned)
    // If already playing, do nothing
    public void PlaySettingsLoop()
    {
        if (settingsLoopClip == null) return;

        if (loopSource == null)
        {
            // Create a new audio source for looping sounds
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.clip = settingsLoopClip;
            loopSource.loop = true;
            loopSource.playOnAwake = false;
            loopSource.volume = volume;
        }

        if (!loopSource.isPlaying)
            loopSource.Play();
    }

    // Stop the loop sound
    public void StopSettingsLoop()
    {
        if (loopSource == null) return;
        if (loopSource.isPlaying)
            loopSource.Stop();
    }
}


