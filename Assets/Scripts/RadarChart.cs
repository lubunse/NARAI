using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RadarChart : MaskableGraphic
{
    [Header("Structure")]
    [Range(1, 12)]
    public int axesCount = 4; // Number of axes in the radar chart

    [Tooltip("Number of steps (rings). For 20% steps = 5")]
    public int steps = 5; // Number of rings in the background grid

    [Header("Appearance")]
    public Color valueFillColor = new Color(0f, 0.6f, 0.2f, 0.45f); // Fill color of the value polygon
    public Color valueOutlineColor = new Color(0f, 0.6f, 0.2f, 1f); // Outline color of the value polygon
    public Color gridColor = new Color(0.7f, 0.7f, 0.7f, 0.18f);    // Color of the background grid
    public Color axisColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);     // Color of the axis lines

    [Tooltip("Line thickness for ALL lines (grid, axes, outline).")]
    public float outlineThickness = 0.1f; // Thickness of grid, axis, and outline lines

    [Range(0f, 0.45f)] public float innerPadding = 0.05f; // Padding inside the chart

    [Header("Labels (assign manually in Inspector)")]
    public List<TextMeshProUGUI> axisLabelObjects = new List<TextMeshProUGUI>(); // Labels for each axis
    public List<TextMeshProUGUI> stepLabelObjects = new List<TextMeshProUGUI>(); // Labels for step values

    public List<string> axisLabels = new List<string>() { "Health", "Social Impact", "Animal Welfare", "Environment" }; // Default axis labels

    [Header("Step Label Appearance")]
    public bool showStepLabels = true;      // Toggle step labels on/off
    public Color stepLabelColor = Color.gray;
    public float stepLabelFontSize = 12f;

    [Header("Animation")]
    public bool enableAnimation = true;     // Toggle value animation
    public float animationDuration = 0.6f;  // Duration of animation in seconds

    [SerializeField]
    private float[] values; // Current values for each axis

    private Coroutine animCoroutine; // Reference to animation coroutine

    protected override void Awake()
    {
        base.Awake();
        EnsureArraySizes();
        ValidateManualLabels();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureArraySizes();
        ValidateManualLabels();
        SetVerticesDirty(); // Mark mesh as dirty to update in editor
    }
#endif

    private void EnsureArraySizes()
    {
        // Ensure values array matches number of axes
        if (values == null || values.Length != axesCount)
            values = new float[axesCount];
        if (axisLabels == null)
            axisLabels = new List<string>();
        while (axisLabels.Count < axesCount)
            axisLabels.Add("Axis " + (axisLabels.Count + 1));
    }

    private void ValidateManualLabels()
    {
        // Check if manually assigned labels match counts (warnings disabled)
    }

    // Set chart values, with optional animation
    public void SetValues(float[] v, bool animate = false)
    {
        if (v == null)
            v = new float[axesCount];

        float[] target = new float[axesCount];
        for (int i = 0; i < axesCount; i++)
        {
            if (i < v.Length)
                target[i] = Mathf.Clamp(v[i], 0f, 100f);
            else
                target[i] = 0f;
        }

        // Stop existing animation if active
        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }

        // Animate values if enabled
        if (enableAnimation && animate && animationDuration > 0.001f)
        {
            float[] start = new float[axesCount];
            Array.Copy(values, start, axesCount);
            animCoroutine = StartCoroutine(AnimateValues(start, target, animationDuration));
        }
        else
        {
            Array.Copy(target, values, axesCount);
            SetVerticesDirty();
        }
    }

    // Coroutine to animate values smoothly over time
    private IEnumerator AnimateValues(float[] from, float[] to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float factor = Mathf.Clamp01(t / duration);
            for (int i = 0; i < axesCount; i++)
            {
                values[i] = Mathf.Lerp(from[i], to[i], EaseOutCubic(factor));
            }
            SetVerticesDirty();
            yield return null;
        }
        Array.Copy(to, values, axesCount);
        SetVerticesDirty();
        animCoroutine = null;
    }

    // Ease-out cubic easing function for smooth animation
    private float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    // Draw the radar chart mesh
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        EnsureArraySizes();

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f * (1f - innerPadding);

        if (axesCount < 1) axesCount = 1;

        // Draw grid rings
        for (int step = 1; step <= steps; step++)
        {
            float stepRadius = radius * (step / (float)steps);
            AddPolygonOutline(vh, center, stepRadius, axesCount, gridColor, outlineThickness);
        }

        // Draw axes lines
        for (int a = 0; a < axesCount; a++)
        {
            float angle = (2 * Mathf.PI / axesCount) * a - Mathf.PI / 2;
            Vector2 end = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            AddLine(vh, center, end, axisColor, outlineThickness);
        }

        // Compute polygon points for current values
        List<Vector2> valueVerts = new List<Vector2>(axesCount);
        for (int a = 0; a < axesCount; a++)
        {
            float t = Mathf.Clamp01(values[a] / 100f);
            float angle = (2 * Mathf.PI / axesCount) * a - Mathf.PI / 2;
            Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * t;
            valueVerts.Add(p);
        }

        // Draw filled polygon and outline
        AddFilledPolygon(vh, center, valueVerts, valueFillColor);
        AddPolygonOutline(vh, center, radius, axesCount, valueOutlineColor, outlineThickness, valueVerts);

        // Update labels
        UpdateAxisLabelObjects(center, radius);
        UpdateStepLabelObjects(center, radius);
    }

    // Add filled polygon to mesh
    private void AddFilledPolygon(VertexHelper vh, Vector2 center, List<Vector2> verts, Color fillColor)
    {
        int baseIndex = vh.currentVertCount;
        UIVertex vc = UIVertex.simpleVert; vc.color = fillColor; vc.position = center; vh.AddVert(vc);

        foreach (var p in verts)
        {
            UIVertex v = UIVertex.simpleVert; v.color = fillColor; v.position = p; vh.AddVert(v);
        }

        int n = verts.Count;
        for (int i = 0; i < n; i++)
        {
            int i1 = baseIndex + 1 + i;
            int i2 = baseIndex + 1 + ((i + 1) % n);
            vh.AddTriangle(baseIndex, i1, i2);
        }
    }

    // Add polygon outline (grid or value outline)
    private void AddPolygonOutline(VertexHelper vh, Vector2 center, float radius, int sides, Color col, float lineWidth, List<Vector2> overrideVerts = null)
    {
        List<Vector2> outer = new List<Vector2>(sides);
        if (overrideVerts != null && overrideVerts.Count == sides)
            outer.AddRange(overrideVerts);
        else
        {
            for (int i = 0; i < sides; i++)
            {
                float angle = (2 * Mathf.PI / sides) * i - Mathf.PI / 2;
                outer.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        for (int i = 0; i < sides; i++)
        {
            Vector2 a = outer[i];
            Vector2 b = outer[(i + 1) % sides];
            AddLine(vh, a, b, col, lineWidth);
        }
    }

    // Add a single line between two points
    private void AddLine(VertexHelper vh, Vector2 a, Vector2 b, Color color, float width)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 n = new Vector2(-dir.y, dir.x) * (width * 0.5f);

        UIVertex v1 = UIVertex.simpleVert; v1.color = color; v1.position = a + n;
        UIVertex v2 = UIVertex.simpleVert; v2.color = color; v2.position = a - n;
        UIVertex v3 = UIVertex.simpleVert; v3.color = color; v3.position = b - n;
        UIVertex v4 = UIVertex.simpleVert; v4.color = color; v4.position = b + n;

        int baseIndex = vh.currentVertCount;
        vh.AddVert(v1); vh.AddVert(v2); vh.AddVert(v3); vh.AddVert(v4);
        vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
        vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
    }

    // Position and update axis label objects
    private void UpdateAxisLabelObjects(Vector2 center, float radius)
    {
        if (axisLabelObjects == null || axisLabelObjects.Count == 0)
            return;

        int count = Mathf.Min(axisLabelObjects.Count, axesCount);
        for (int i = 0; i < count; i++)
        {
            var lab = axisLabelObjects[i];
            if (lab == null) continue;

            float angle = (2 * Mathf.PI / axesCount) * i - Mathf.PI / 2;
            float cos = Mathf.Cos(angle);

            Vector2 pos;
            if (cos > 0.5f || cos < -0.5f) pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (1.7f);  // right/left
            else pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (1.1f);  // top/bottom

            lab.rectTransform.anchoredPosition = pos;

            if (i < axisLabels.Count && !string.IsNullOrEmpty(axisLabels[i]))
                lab.text = axisLabels[i];

            // Adjust text alignment based on position
            if (cos > 0.5f) lab.alignment = TextAlignmentOptions.Left;   // right side
            else if (cos < -0.5f) lab.alignment = TextAlignmentOptions.Right;  // left side
            else lab.alignment = TextAlignmentOptions.Center; // top/bottom
        }
    }

    // Position and update step label objects
    private void UpdateStepLabelObjects(Vector2 center, float radius)
    {
        if (!showStepLabels || stepLabelObjects == null || stepLabelObjects.Count == 0)
            return;

        // Only show labels at 25%, 50%, 75%
        int[] importantSteps = { 25, 50, 75 };

        int count = Mathf.Min(stepLabelObjects.Count, importantSteps.Length);
        for (int i = 0; i < count; i++)
        {
            var lab = stepLabelObjects[i];
            if (lab == null) continue;

            int percent = importantSteps[i];
            float stepRadius = radius * (percent / 100f);

            Vector2 pos = new Vector2(0f, stepRadius);
            lab.rectTransform.anchoredPosition = pos;
            lab.text = percent + "%";
            lab.color = stepLabelColor;
            lab.fontSize = stepLabelFontSize;
            lab.alignment = TextAlignmentOptions.Center;
        }
    }

    // Set axis labels dynamically
    public void SetAxisLabels(List<string> labels)
    {
        axisLabels = labels ?? new List<string>();
        if (axisLabelObjects != null && axisLabelObjects.Count > 0)
        {
            for (int i = 0; i < axisLabelObjects.Count && i < axisLabels.Count; i++)
            {
                var t = axisLabelObjects[i];
                if (t == null) continue;
                t.text = axisLabels[i];
            }
        }
        SetVerticesDirty();
    }

    // Return a copy of current values
    public float[] GetValues()
    {
        EnsureArraySizes();
        float[] copy = new float[axesCount];
        Array.Copy(values, copy, axesCount);
        return copy;
    }
}
