using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class AnalyzeProduct : MonoBehaviour
{
    // Singleton instance
    public static AnalyzeProduct Instance;

    [Header("AR")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private ARAnchorManager anchorManager;
    [SerializeField] private Camera arCamera;

    [Header("OpenAI Settings")]
    [SerializeField] private string openAiApiKey = "YOUR_API_KEY"; // OpenAI API key
    [SerializeField] private string model = "gpt-5-mini"; // OpenAI model to use

    [Header("UI")]
    public GameObject CanvasAnalysisResultPrefab; // Prefab for AR analysis canvas
    public Button DeleteCanvasButton; // Button to delete all existing AR canvases
    public Canvas AnalyzingCanvas; // Canvas shown during analysis
    public Image analyzingProgressImage; // Image with fillAmount between 0 and 1 for progress
    public float analyzingDuration = 5f; // Duration of progress animation in seconds
    public Button scanButton; // Scan button to start analysis
    public Button cancelButton; // Cancel button to abort analysis

    [Header("Error Handling")]
    public Canvas ErrorCanvas; // Canvas to show errors
    public TextMeshProUGUI ErrorText; // TMP field for error messages
    public float errorDisplayTime = 5f; // Time to display error messages

    private const string CANVAS_TAG = "ARCanvas"; // Tag used for instantiated AR canvases

    // --- New state management variables for cancel system ---
    private UnityWebRequest activeRequest; // Holds reference to the ongoing request
    private Coroutine activeAnalysisCoroutine; // Holds reference to the running analysis coroutine
    private bool isCanceled = false; // Tracks if the user canceled the analysis

    private void Awake()
    {
        // Ensure only one instance exists
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Delete any previous AR canvases
        DeleteAllCanvases();
    }

    // Start analysis for a given image
    public void AnalyzeImage(string base64Image, Vector3 position, Quaternion rotation)
    {
        // --- UI Setup for cancel system ---
        scanButton.gameObject.SetActive(false);      // Hide scan button
        cancelButton.gameObject.SetActive(true);     // Show cancel button
        cancelButton.interactable = true;
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(CancelAnalysis);

        if (AnalyzingCanvas != null) AnalyzingCanvas.gameObject.SetActive(true);

        if (analyzingProgressImage != null)
        {
            analyzingProgressImage.fillAmount = 0f;
            StartCoroutine(UpdateProgressImage()); // Animate progress bar
        }

        // Start coroutine and keep reference (for cancel)
        activeAnalysisCoroutine = StartCoroutine(SendImageToOpenAI(base64Image, position, rotation));
    }

    // Cancel system
    private void CancelAnalysis()
    {
        Debug.Log("Analysis canceled by user.");

        // Play cancel button sound
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonClick();

        isCanceled = true;

        // Stop running coroutine
        if (activeAnalysisCoroutine != null)
        {
            StopCoroutine(activeAnalysisCoroutine);
            activeAnalysisCoroutine = null;
        }

        // Abort ongoing request if possible
        if (activeRequest != null)
        {
            try { activeRequest.Abort(); } catch { }
            activeRequest = null;
        }

        // Hide analyzing canvas
        if (AnalyzingCanvas != null)
            AnalyzingCanvas.gameObject.SetActive(false);

        // Reset UI after short delay
        StartCoroutine(ResetAfterCancel());
    }

    private IEnumerator ResetAfterCancel()
    {
        cancelButton.interactable = false;

        yield return new WaitForSeconds(1.5f);

        cancelButton.gameObject.SetActive(false);
        scanButton.gameObject.SetActive(true);
        scanButton.interactable = true;
        isCanceled = false;

        Debug.Log("System ready for next scan.");
    }

    // Animate the analyzing progress image over analyzingDuration
    private IEnumerator UpdateProgressImage()
    {
        float elapsed = 0f;
        while (elapsed < analyzingDuration)
        {
            elapsed += Time.deltaTime;
            if (analyzingProgressImage != null)
            {
                analyzingProgressImage.fillAmount = Mathf.Clamp01(elapsed / analyzingDuration);
            }
            yield return null;
        }
    }

    // Coroutine to send the image to OpenAI and handle response
    private IEnumerator SendImageToOpenAI(string base64Image, Vector3 worldPosition, Quaternion worldRotation)
    {
        string imageDataUrl = "data:image/png;base64," + base64Image;

        // Formatted prompt for OpenAI
        string promptText = @"
        You are a food product analysis assistant. You will receive an image of a packaged food product. 
        Analyze the product based on visible packaging information AND general knowledge (including your training data). 
        If certain data is not visible or unclear, make a best possible estimate based on typical values for the same product type or known brand products. 
        Only mark values as ""unknown"" if there is truly no reasonable guess possible. 

        If no recognizable food product is detected in the image, respond with exactly this string (without JSON): ""No Product Detected"", else respond with the JSON object below.

        Strict rules:
        - Return ONLY a valid JSON object in your answer.
        - Do not include code block markers, text, or explanations outside of the JSON.
        - All numeric values must be finite numbers. Never output NaN, Infinity, or -Infinity.
        - Use a dot as decimal separator for floats (e.g. 12.5).
        - Percent values must be integers between 0 and 100.
        - All strings must be in English.
        - Respond ONLY with valid JSON, do not add any explanation or extra text.
        - If several foods are recognizable, select the most relevant one.

        JSON schema:
        {
            ""product_name"": ""string"",
            ""manufacturer"": ""string"",
            ""recommended_retail_price"": ""string"",
            ""is_beverage"": boolean,
            ""nutri_score"": ""string"",
            ""nutrition"": {
                ""per_100g"": {
                    ""calories_kcal"": number,
                    ""energy_kj"": number,
                    ""fat_g"": number,
                    ""saturated_fat_g"": number,
                    ""sugar_g"": number,
                    ""salt_g"": number
                }
            },
            ""sustainability"": {
                ""health_percent"": number,
                ""social_aspects_percent"": number,
                ""animal_welfare_percent"": number,
                ""environment_percent"": number
            },
            ""allergens"": [""string"", ""string"", ""...""],
            ""general_pros_cons"": {
                ""pros"": [""string"", ""string"", ""string""],
                ""cons"": [""string"", ""string"", ""string""]
            }
        }

        Slide explanations:
        - Slide 1 (Overview): Recommended retail price in euro without € symbol (estimated if not visible), is_beverage (true if beverage, false if solid/food) and 
                              only for packaged products: Nutri-Score (A-E according to the new calculation algorithm); if it is not a packaged food, return X.
        - Slide 2 (Nutrition Values): Show ONLY per_100g numbers. Its very important that the values are correct! Be sure to return sugar, not carbohydrates.
        - Slide 3 (Sustainability): Percent values (The higher the percentage, the more the category is fulfilled: higher = better) estimating 
                                    health (Health-promoting food consumption that contributes to higher life
                                        expectancy, higher healthy life expectancy and greater well-being for all), 
                                    social impact (Food consumption that guarantees minimum social stan-
                                        dards along value chains), 
                                    animal welfare (Food consumption that supports greater animal welfare
                                        and thus meets the changing ethical demands of society; if the product is vegetarian or vegan the value should be high!),
                                    environmental impact (Environmentally and climate-friendly food consumption that
                                        fits in with Germany’s medium and long-term sustainability goals).
        - Slide 4 (Possible Allergens): Known or likely allergens (only the allergen, no explanation).
        - Slide 5 (Summary): Key positive and negative aspects (only 1 or 2 words).
    ";

        // Build request payload for GPT-5-mini
        var requestPayload = new
        {
            model = model, // "gpt-5-mini"
            input = new object[]
            {
            new {
                role = "user",
                content = new object[]
                {
                    new { type = "input_text", text = promptText },
                    new { type = "input_image", image_url = imageDataUrl }
                }
            }
            },
            reasoning = new { effort = "minimal" },
            stream = false
        };

        string jsonBody = JsonConvert.SerializeObject(requestPayload, Formatting.None);

        // Create and configure UnityWebRequest
        activeRequest = new UnityWebRequest("https://api.openai.com/v1/responses", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        activeRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        activeRequest.downloadHandler = new DownloadHandlerBuffer();
        activeRequest.SetRequestHeader("Content-Type", "application/json");
        activeRequest.SetRequestHeader("Authorization", $"Bearer {openAiApiKey}");
        activeRequest.timeout = 60;
        activeRequest.chunkedTransfer = false;

        // Send request
        yield return activeRequest.SendWebRequest();

        // Stop here if canceled
        if (isCanceled)
        {
            Debug.Log("Analysis coroutine ended early due to cancel.");
            yield break;
        }

        // Restore UI state
        cancelButton.gameObject.SetActive(false);
        scanButton.gameObject.SetActive(true);
        scanButton.interactable = true;

        // Handle request error
        if (activeRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("OpenAI Error: " + activeRequest.error);
            Debug.LogError("Response Text: " + activeRequest.downloadHandler.text);
            ShowError("OpenAI Error: " + activeRequest.error);
            if (AnalyzingCanvas != null) AnalyzingCanvas.gameObject.SetActive(false);
            yield break;
        }

        string responseText = activeRequest.downloadHandler.text;
        activeRequest = null;

        Debug.Log("Raw OpenAI Response: " + responseText);

        // Parse GPT response
        string jsonResult = ParseChatGPTResponse(responseText);

        // Handle missing or empty result
        if (string.IsNullOrEmpty(jsonResult))
        {
            ShowError("No valid response received from GPT-5-mini.");
            Debug.LogError("Empty or invalid JSON result");
            if (AnalyzingCanvas != null) AnalyzingCanvas.gameObject.SetActive(false);
            yield break;
        }

        // Handle "No Product Detected"
        if (jsonResult.Trim().Equals("No Product Detected", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("No Product Detected");
            ShowError("No Product Detected. Try again!");
            if (AnalyzingCanvas != null) AnalyzingCanvas.gameObject.SetActive(false);
            yield break;
        }

        // Deserialize and display result
        try
        {
            ProductAnalysis analysis = JsonConvert.DeserializeObject<ProductAnalysis>(jsonResult);
            if (analysis == null)
            {
                ShowError("Deserialization failed: Empty result.");
                Debug.LogError("Deserialization returned null.");
                yield break;
            }

            SoundManager.Instance.PlayAnalysisComplete();
            ShowARCanvas(analysis, worldPosition, worldRotation);
        }
        catch (Exception e)
        {
            Debug.LogError("Error parsing JSON: " + e.Message);
            ShowError("Error parsing response: " + e.Message);
            if (AnalyzingCanvas != null) AnalyzingCanvas.gameObject.SetActive(false);
        }
    }

    private string ParseChatGPTResponse(string json)
    {
        try
        {
            JObject jObj = JObject.Parse(json);

            // Handle GPT-5 /v1/responses format
            JArray outputArray = jObj["output"] as JArray;
            if (outputArray != null)
            {
                foreach (var item in outputArray)
                {
                    string type = item["type"]?.ToString();

                    // Look specifically for the "message" object containing the output text
                    if (type == "message")
                    {
                        JArray contentArray = item["content"] as JArray;
                        if (contentArray != null)
                        {
                            foreach (var content in contentArray)
                            {
                                string contentType = content["type"]?.ToString();
                                if (contentType == "output_text")
                                {
                                    string text = content["text"]?.ToString()?.Trim();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        Debug.Log("Parsed GPT-5 message output successfully");
                                        return text;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Fallback for models like GPT-4 that use the "choices" structure
            if (jObj["choices"] != null)
            {
                string legacy = jObj["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(legacy))
                {
                    Debug.Log("Parsed GPT-4 style response successfully");
                    return legacy;
                }
            }

            Debug.LogWarning("Response has no valid output_text or choices field.");
            ShowError("Response format not recognized. No output_text field found.");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError("Error parsing GPT response: " + e.Message);
            ShowError("Error parsing GPT response: " + e.Message);
            return null;
        }
    }

    private void ShowARCanvas(ProductAnalysis analysis, Vector3 position, Quaternion rotation)
    {
        if (AnalyzingCanvas != null) AnalyzingCanvas.gameObject.SetActive(false);

        if (CanvasAnalysisResultPrefab == null)
        {
            Debug.LogError("CanvasAnalysisResultPrefab is not assigned.");
            ShowError("CanvasAnalysisResultPrefab not assigned.");
            return;
        }

        if (analyzingProgressImage != null)
            analyzingProgressImage.fillAmount = 0f;

        if (DeleteCanvasButton != null)
            DeleteCanvasButton.gameObject.SetActive(true);

        // Create a pose at the desired world position
        Pose pose = new Pose(position, rotation);

        // Ask ARAnchorManager to create a *real tracked* anchor
        ARAnchor anchor = null;
        var anchorTask = anchorManager.TryAddAnchorAsync(pose);
        if (anchorTask != null)
        {
            var result = anchorTask.GetAwaiter().GetResult();
            if (result.status.IsSuccess())
                anchor = result.value;
        }

        // Instantiate Canvas and parent it to the anchor
        GameObject canvas = Instantiate(CanvasAnalysisResultPrefab, pose.position, pose.rotation);
        if (anchor == null)
        {
            Debug.LogWarning("Falling back: No valid anchor, instantiating canvas without anchor.");
            canvas.transform.SetParent(null, worldPositionStays: true);
        }
        else
        {
            canvas.transform.SetParent(anchor.transform, worldPositionStays: true);
        }

        canvas.tag = CANVAS_TAG;

        // Setup world camera
        Canvas canvasComponent = canvas.GetComponent<Canvas>();
        if (canvasComponent != null)
            canvasComponent.worldCamera = Camera.main;

        // Get SlideContainer
        Transform slideContainer = canvas.transform.Find("Panel/Viewport/SlideContainer");
        if (slideContainer == null)
        {
            Debug.LogError("SlideContainer not found.");
            ShowError("SlideContainer not found.");
            return;
        }

        // Slide 1 - Overview
        string productName = !string.IsNullOrEmpty(analysis.product_name) ? analysis.product_name : "Unknown product";
        string manufacturer = !string.IsNullOrEmpty(analysis.manufacturer) ? analysis.manufacturer : "Unknown";
        string recommended_retail_price = !string.IsNullOrEmpty(analysis.recommended_retail_price) ? (analysis.recommended_retail_price != "Unknown" ? analysis.recommended_retail_price + "€" : analysis.recommended_retail_price) : "Unknown";
        string nutriScore = !string.IsNullOrEmpty(analysis.nutri_score) ? GetScore(analysis.nutri_score) : "-";

        string slide1Text =
            "<b><size=120%><u>Overview</u></size></b>\n\n" +
            $"<b>Product:</b> {productName}\n" +
            $"<b>Manufacturer:</b> {manufacturer}\n" +
            $"<b>RRP:</b> {recommended_retail_price}\n" +
            $"<b>Score:</b> {nutriScore}";

        SetSlideText(slideContainer, "Slide1", "Text1", slide1Text);

        // Slide 2 - Nutrition per 100g or 100ml with traffic-light coloring
        string nutritionText;
        if (analysis.nutrition != null && analysis.nutrition.per_100g != null)
        {
            var p = analysis.nutrition.per_100g;
            bool isBeverage = analysis.is_beverage;

            string header = isBeverage ? "<b><u>Nutrition per 100ml</u></b>\n" : "<b><u>Nutrition per 100g</u></b>\n";

            string kcalStr = (p.calories_kcal > 0f) ? $"{FormatNumber(p.calories_kcal)} kcal" : "unknown";
            string kjStr = (p.energy_kj > 0f) ? $"{FormatNumber(p.energy_kj)} kJ" : "unknown";
            string energyLine = $"Energy: {kcalStr} / {kjStr}\n\n";

            string fatColor = GetTrafficLightColorHex_Full(nutrient: "fat_g", value: p.fat_g, isBeverage: isBeverage);
            string satColor = GetTrafficLightColorHex_Full(nutrient: "saturated_fat_g", value: p.saturated_fat_g, isBeverage: isBeverage);
            string sugarColor = GetTrafficLightColorHex_Full(nutrient: "sugar_g", value: p.sugar_g, isBeverage: isBeverage);
            string saltColor = GetTrafficLightColorHex_Full(nutrient: "salt_g", value: p.salt_g, isBeverage: isBeverage);

            nutritionText =
                header +
                energyLine +
                $"Fat: <color={fatColor}>{FormatNumber(p.fat_g)} g</color>\n" +
                $"Saturated fat: <color={satColor}>{FormatNumber(p.saturated_fat_g)} g</color>\n" +
                $"Sugar: <color={sugarColor}>{FormatNumber(p.sugar_g)} g</color>\n" +
                $"Salt: <color={saltColor}>{FormatNumber(p.salt_g)} g</color>";
        }
        else
        {
            nutritionText = "<b><u>Nutrition per 100g</u></b>\nNo nutrition information available";
        }
        SetSlideText(slideContainer, "Slide2", "Text2", nutritionText);

        // Slide 3 - Sustainability values
        Transform slide3Transform = slideContainer.Find("Slide3");
        if (slide3Transform == null)
        {
            // Fallback: display as text
            string sustainabilityText = "<b><u>Sustainability</u></b>\n" +
                (analysis.sustainability != null
                    ? $"Health: {analysis.sustainability.health_percent}%\nSocial: {analysis.sustainability.social_aspects_percent}%\nAnimal welfare: {analysis.sustainability.animal_welfare_percent}%\nEnvironment: {analysis.sustainability.environment_percent}%"
                    : "No sustainability information available");
            SetSlideText(slideContainer, "Slide3", "Text3", sustainabilityText);
        }
        else
        {
            // Find RadarChart under Slide3
            Transform radarGO = slide3Transform.Find("RadarChart");
            if (radarGO == null)
            {
                // Fallback: display as text
                string sustainabilityText = "<b><u>Sustainability</u></b>\n" +
                    (analysis.sustainability != null
                        ? $"Health: {analysis.sustainability.health_percent}%\nSocial: {analysis.sustainability.social_aspects_percent}%\nAnimal welfare: {analysis.sustainability.animal_welfare_percent}%\nEnvironment: {analysis.sustainability.environment_percent}%"
                        : "No sustainability information available");
                SetSlideText(slideContainer, "Slide3", "Text3", sustainabilityText);
            }
            else
            {
                var radar = radarGO.GetComponent<RadarChart>();
                if (radar == null)
                {
                    // Fallback: display as text
                    string sustainabilityText = "<b><u>Sustainability</u></b>\n" +
                        (analysis.sustainability != null
                            ? $"Health: {analysis.sustainability.health_percent}%\nSocial: {analysis.sustainability.social_aspects_percent}%\nAnimal welfare: {analysis.sustainability.animal_welfare_percent}%\nEnvironment: {analysis.sustainability.environment_percent}%"
                            : "No sustainability information available");
                    SetSlideText(slideContainer, "Slide3", "Text3", sustainabilityText);
                }
                else
                {
                    // Set radar chart values (0..100), animate
                    float h = (analysis.sustainability != null) ? analysis.sustainability.health_percent : 0f;
                    float s = (analysis.sustainability != null) ? analysis.sustainability.social_aspects_percent : 0f;
                    float a = (analysis.sustainability != null) ? analysis.sustainability.animal_welfare_percent : 0f;
                    float e = (analysis.sustainability != null) ? analysis.sustainability.environment_percent : 0f;

                    radar.SetValues(new float[] { h, s, a, e }, true);
                }
            }
        }

        // Slide 4 - Allergens
        // Map allergen keywords to TMP sprite codes (Sprite names from your assets)
        Dictionary<string, string> allergenEmojis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 1. Gluten-containing grains 🌾
            { "gluten", "<sprite name=\"gluten\">" },
            { "wheat", "<sprite name=\"gluten\">" },
            { "rye", "<sprite name=\"gluten\">" },
            { "barley", "<sprite name=\"gluten\">" },
            { "oats", "<sprite name=\"gluten\">" },
            { "spelt", "<sprite name=\"gluten\">" },
            { "kamut", "<sprite name=\"gluten\">" },

            // 2. Crustaceans 🦐
            { "crustaceans", "<sprite name=\"shrimp\">" },
            { "crustacean", "<sprite name=\"shrimp\">" },
            { "shrimps", "<sprite name=\"shrimp\">" },
            { "shrimp", "<sprite name=\"shrimp\">" },
            { "prawns", "<sprite name=\"shrimp\">" },
            { "prawn", "<sprite name=\"shrimp\">" },
            { "lobster", "<sprite name=\"lobster\">" },
            { "lobsters", "<sprite name=\"lobster\">" },
            { "crayfish", "<sprite name=\"crayfish\">" },
            { "crayfishs", "<sprite name=\"crayfish\">" },
            { "crawfish", "<sprite name=\"crayfish\">" },
            { "crawfishs", "<sprite name=\"crayfish\">" },
            { "crab", "<sprite name=\"crab\">" },
            { "crabs", "<sprite name=\"crab\">" },

            // 3. Eggs 🥚
            { "egg", "<sprite name=\"egg\">" },
            { "eggs", "<sprite name=\"egg\">" },

            // 4. Fish 🐟
            { "fish", "<sprite name=\"fish\">" },
            { "anchovy", "<sprite name=\"fish\">" },
            { "anchovies", "<sprite name=\"fish\">" },
            { "bass", "<sprite name=\"fish\">" },
            { "catfish", "<sprite name=\"fish\">" },
            { "herring", "<sprite name=\"fish\">" },
            { "mackerel", "<sprite name=\"fish\">" },
            { "trout", "<sprite name=\"fish\">" },
            { "salmonid", "<sprite name=\"fish\">" },
            { "salmonella", "<sprite name=\"fish\">" },
            { "salmon", "<sprite name=\"fish\">" },
            { "tuna", "<sprite name=\"fish\">" },
            { "cod", "<sprite name=\"fish\">" },

            // 5. Peanuts 🥜
            { "peanut", "<sprite name=\"peanut\">" },
            { "peanuts", "<sprite name=\"peanut\">" },

            // 6. Soy 🌱
            { "soy", "<sprite name=\"soy\">" },
            { "soya", "<sprite name=\"soy\">" },

            // 7. Milk 🥛 / Cheese 🧀
            { "milk", "<sprite name=\"milk\">" },
            { "lactose", "<sprite name=\"milk\">" },
            { "dairy", "<sprite name=\"milk\">" },
            { "cheese", "<sprite name=\"cheese\">" },

            // 8. Tree nuts 🌰
            { "nuts", "<sprite name=\"nut\">" },
            { "tree nuts", "<sprite name=\"nut\">" },
            { "almond", "<sprite name=\"nut\">" },
            { "almonds", "<sprite name=\"nut\">" },
            { "hazelnut", "<sprite name=\"nut\">" },
            { "hazelnuts", "<sprite name=\"nut\">" },
            { "walnut", "<sprite name=\"nut\">" },
            { "walnuts", "<sprite name=\"nut\">" },
            { "cashew", "<sprite name=\"nut\">" },
            { "cashews", "<sprite name=\"nut\">" },
            { "pecan", "<sprite name=\"nut\">" },
            { "pecans", "<sprite name=\"nut\">" },
            { "brazil nut", "<sprite name=\"nut\">" },
            { "brazil nuts", "<sprite name=\"nut\">" },
            { "pistachio", "<sprite name=\"nut\">" },
            { "pistachios", "<sprite name=\"nut\">" },
            { "macadamia", "<sprite name=\"nut\">" },

            // 9. Celery 🥬
            { "celery", "<sprite name=\"celery\">" },

            // 10. Mustard 🌿
            { "mustard", "<sprite name=\"mustard\">" },

            // 11. Sesame ⚪
            { "sesame", "<sprite name=\"sesame\">" },

            // 12. Sulphites 🧪
            { "sulphite", "<sprite name=\"sulphite\">" },
            { "sulfite", "<sprite name=\"sulphite\">" },
            { "sulphur dioxide", "<sprite name=\"sulphite\">" },

            // 13. Lupin 🌸
            { "lupin", "<sprite name=\"lupin\">" },

            // 14. Mollusks 🐚 🦪 🦑 🐙
            { "mollusc", "<sprite name=\"shell\">" },
            { "mussels", "<sprite name=\"shell\">" },
            { "clam", "<sprite name=\"shell\">" },
            { "oyster", "<sprite name=\"oyster\">" },
            { "scallop", "<sprite name=\"shell\">" },
            { "abalone", "<sprite name=\"shell\">" },
            { "snail", "<sprite name=\"snail\">" },
            { "snails", "<sprite name=\"snail\">" },
            { "cuttlefish", "<sprite name=\"cuttlefish\">" },
            { "cuttlefishs", "<sprite name=\"cuttlefish\">" },
            { "squid", "<sprite name=\"squid\">" },
            { "squids", "<sprite name=\"squid\">" },
            { "calamari", "<sprite name=\"squid\">" },
            { "octopus", "<sprite name=\"octopus\">" },
            { "octopi", "<sprite name=\"octopus\">" }
        };

        // Build text with emojis
        List<string> allergensWithEmoji = new List<string>();
        if (analysis.allergens != null && analysis.allergens.Length > 0)
        {
            foreach (string allergen in analysis.allergens)
            {
                string emoji = allergenEmojis.ContainsKey(allergen) ? allergenEmojis[allergen] + " " : "";
                allergensWithEmoji.Add(emoji + allergen);
            }
        }

        string allergensText =
            "<b><u>Possible Allergens</u></b>\n" +
            (allergensWithEmoji.Count > 0 ? string.Join("\n", allergensWithEmoji) : "No allergen data available");

        SetSlideText(slideContainer, "Slide4", "Text4", allergensText);

        // Slide 5 - Summary with pros and cons
        string[] pros = (analysis.general_pros_cons?.pros != null && analysis.general_pros_cons.pros.Length > 0)
            ? analysis.general_pros_cons.pros
            : new string[] { "No positive aspects available" };

        string[] cons = (analysis.general_pros_cons?.cons != null && analysis.general_pros_cons.cons.Length > 0)
            ? analysis.general_pros_cons.cons
            : new string[] { "No negative aspects available" };

        // Format pros and cons text with color
        string prosText = "<b><u><color=#000000>Pros</color></u></b>\n" +
                          string.Join("\n", Array.ConvertAll(pros, p => $"<color=#006400>{p}</color>"));

        string consText = "<b><u><color=#000000>Cons</color></u></b>\n" +
                          string.Join("\n", Array.ConvertAll(cons, c => $"<color=#8B0000>{c}</color>"));

        Transform summaryContainer = slideContainer.Find("Slide5/SummaryContainer");
        if (summaryContainer != null)
        {
            var prosField = summaryContainer.Find("ProsText");
            var consField = summaryContainer.Find("ConsText");
            if (prosField != null && prosField.GetComponent<TextMeshProUGUI>() != null)
                prosField.GetComponent<TextMeshProUGUI>().text = prosText;
            else
                Debug.LogWarning("ProsText field not found on Slide5.");

            if (consField != null && consField.GetComponent<TextMeshProUGUI>() != null)
                consField.GetComponent<TextMeshProUGUI>().text = consText;
            else
                Debug.LogWarning("ConsText field not found on Slide5.");
        }
        else
        {
            Debug.LogWarning("SummaryContainer not found on Slide5.");
        }
    }

    // Format float numbers as string with dot as decimal separator
    private string FormatNumber(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // Determine traffic-light color hex for nutrient values
    private string GetTrafficLightColorHex_Full(string nutrient, float value, bool isBeverage)
    {
        if (float.IsNaN(value))
            return "#808080"; // gray for invalid value

        float greenThreshold = float.NaN;
        float redThreshold = float.NaN;

        if (!isBeverage)
        {
            // Food thresholds
            switch (nutrient)
            {
                case "fat_g":
                    greenThreshold = 3f; // fallback
                    redThreshold = 17.5f;
                    break;
                case "saturated_fat_g":
                    greenThreshold = 1.5f;
                    redThreshold = 5f;
                    break;
                case "sugar_g":
                    greenThreshold = 5f;
                    redThreshold = 22.5f;
                    break;
                case "salt_g":
                    greenThreshold = 0.3f;
                    redThreshold = 1.5f;
                    break;
                default:
                    return "#808080";
            }
        }
        else
        {
            // Beverage thresholds
            switch (nutrient)
            {
                case "fat_g":
                    greenThreshold = 1.5f;
                    redThreshold = 8.75f;
                    break;
                case "saturated_fat_g":
                    greenThreshold = 0.75f;
                    redThreshold = 2.5f;
                    break;
                case "sugar_g":
                    greenThreshold = 2.5f;
                    redThreshold = 11.25f;
                    break;
                case "salt_g":
                    greenThreshold = 0.3f;
                    redThreshold = 1.5f;
                    break;
                default:
                    return "#808080";
            }
        }

        // Return color based on thresholds
        if (value < greenThreshold)
            return "#00A000"; // green
        else if (value > redThreshold)
            return "#D00000"; // red
        else
            return "#FFD100"; // yellow
    }

    // Converts Nutri-Score letter to number
    private string GetScore(string nutriScore)
    {
        if (string.IsNullOrEmpty(nutriScore))
            return "-";
        nutriScore = nutriScore.ToUpper();
        switch (nutriScore)
        {
            case "A": return "<color=#00A000>5</color>/5";      // Green
            case "B": return "<color=#80FF00>4</color>/5";      // Light Green
            case "C": return "<color=#FFD100>3</color>/5";      // Yerrow
            case "D": return "<color=#FFA500>2</color>/5";      // Orange
            case "E": return "<color=#D00000>1</color>/5";      // Red
            case "X": return "Not Applicable";
            default: return "-";
        }
    }

    // Set the text for a specific slide
    private void SetSlideText(Transform slideContainer, string slideName, string textName, string content)
    {
        Transform slide = slideContainer.Find(slideName);
        if (slide == null)
        {
            Debug.LogError($"Slide {slideName} not found.");
            ShowError($"Slide {slideName} not found.");
            return;
        }

        Transform textObj = slide.Find(textName);
        if (textObj == null)
        {
            Debug.LogError($"{textName} in {slideName} not found.");
            ShowError($"{textName} in {slideName} not found.");
            return;
        }

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = content;
        }
        else
        {
            Debug.LogError($"{textName} in {slideName} has no TMP component.");
            ShowError($"{textName} in {slideName} has no TextMeshProUGUI component.");
        }
    }

    // Delete all existing AR canvases
    private void DeleteAllCanvases()
    {
        GameObject[] oldCanvases = GameObject.FindGameObjectsWithTag(CANVAS_TAG);
        foreach (GameObject c in oldCanvases)
        {
            Destroy(c);
        }
    }

    // Show an error message on ErrorCanvas
    public void ShowError(string message)
    {
        scanButton.interactable = false; // Disable scan button

        // Play error sound
        SoundManager.Instance.PlayError();

        if (ErrorCanvas == null || ErrorText == null)
        {
            Debug.LogError("ErrorCanvas or ErrorText is not assigned!");
            return;
        }

        StopCoroutine(nameof(HideErrorAfterDelay));
        ErrorText.text = message;
        if (AnalyzingCanvas != null) AnalyzingCanvas.gameObject.SetActive(false);
        ErrorCanvas.gameObject.SetActive(true);

        StartCoroutine(HideErrorAfterDelay());
    }

    // Hide error after delay
    private IEnumerator HideErrorAfterDelay()
    {
        yield return new WaitForSeconds(errorDisplayTime);
        if (ErrorCanvas != null) ErrorCanvas.gameObject.SetActive(false);

        Image backgroundImage = ErrorCanvas.GetComponentInChildren<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.color = Color.softRed; // set background color to red
        }
        scanButton.interactable = true; // Re-enable scan button
    }

    // OpenAI response data classes
    [Serializable] public class OpenAIResponse { public Choice[] choices; }
    [Serializable] public class Choice { public Message message; }
    [Serializable] public class Message { public string content; }

    // Product Analysis data classes
    [Serializable]
    public class ProductAnalysis
    {
        public string product_name;
        public string manufacturer;
        public string recommended_retail_price;
        public bool is_beverage;
        public string nutri_score;
        public NutritionData nutrition;
        public SustainabilityData sustainability;
        public string[] allergens;
        public ProsConsData general_pros_cons;
    }
    [Serializable] public class NutritionData { public NutritionValues total; public NutritionValues per_100g; }
    [Serializable] public class NutritionValues { public float calories_kcal; public float energy_kj; public float fat_g; public float saturated_fat_g; public float sugar_g; public float salt_g; }
    [Serializable] public class SustainabilityData { public int health_percent; public int social_aspects_percent; public int animal_welfare_percent; public int environment_percent; }
    [Serializable] public class ProsConsData { public string[] pros; public string[] cons; }
}