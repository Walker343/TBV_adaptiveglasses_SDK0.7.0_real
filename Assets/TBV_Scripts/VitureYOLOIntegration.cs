using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class VitureYOLOIntegration : MonoBehaviour
{
    [Header("Components")]
    public VitureRGBCamera vitureCamera;
    public YOLOv8Detector yoloDetector;

    [Header("UI")]
    public TextMeshProUGUI statsText;

    [Header("Detection Settings")]
    public float detectionInterval = 0.1f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private float lastDetectionTime;
    private List<Detection> currentDetections = new List<Detection>();

    // FPS tracking
    private float fpsTimer;
    private int frameCount;
    private float currentFPS;

    void Start()
    {
        Debug.Log("=== VitureYOLOIntegration START ===");

        if (vitureCamera == null)
        {
            Debug.LogError("VitureRGBCamera component not assigned!");
        }
        else
        {
            Debug.Log("VitureRGBCamera assigned OK");
        }

        if (yoloDetector == null)
        {
            Debug.LogError("YOLOv8Detector component not assigned!");
        }
        else
        {
            Debug.Log("YOLOv8Detector assigned OK");
        }
    }

    void Update()
    {
        UpdateFPS();

        if (vitureCamera == null || yoloDetector == null) return;

        // FIX: Added parentheses to call the methods instead of referencing the method group
        if (!vitureCamera.IsReady() || !yoloDetector.IsReady()) return;

        if (Time.time - lastDetectionTime >= detectionInterval)
        {
            RunDetection();
            lastDetectionTime = Time.time;
        }
    }

    void UpdateFPS()
    {
        frameCount++;
        fpsTimer += Time.deltaTime;

        if (fpsTimer >= 1.0f)
        {
            currentFPS = frameCount / fpsTimer;
            frameCount = 0;
            fpsTimer = 0;

            UpdateStatsDisplay();
        }
    }

    void UpdateStatsDisplay()
    {
        if (statsText != null)
        {
            statsText.text = $"FPS: {currentFPS:F1}\nDetections: {currentDetections.Count}";
        }
    }

    void RunDetection()
    {
        Texture2D frame = vitureCamera.GetCurrentFrame();
        if (frame == null)
        {
            Debug.LogWarning("No frame from VitureRGBCamera!");
            return;
        }

        currentDetections = yoloDetector.Detect(frame);

        UpdateStatsDisplay();

        if (showDebugLogs && currentDetections.Count > 0)
        {
            Debug.Log($"Detected {currentDetections.Count} objects:");
            foreach (var det in currentDetections)
            {
                Debug.Log($"  - {det.className} at ({det.bbox.x:F0}, {det.bbox.y:F0}) confidence: {det.confidence:F2}");
            }
        }
    }

    public List<Detection> GetCurrentDetections()
    {
        return new List<Detection>(currentDetections);
    }
}