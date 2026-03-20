using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class VitureYOLOIntegration : MonoBehaviour
{
    [Header("Components")]
    public VitureRGBCamera vitureCamera;
    public Yolov8Detector yoloDetector;

    [Header("UI")]
    public TextMeshProUGUI statsText;

    [Header("Detection Settings")]
    public float detectionInterval = 0.1f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private float lastDetectionTime;
    private List<Detection> currentDetections = new List<Detection>();
    private bool isDetecting = false;

    // FPS tracking
    private float fpsTimer;
    private int frameCount;
    private float currentFPS;

    void Update()
    {
        UpdateFPS();

        // CHECK 1: Wait for Camera initialization (prevents the 2-second startup crash)
        if (vitureCamera == null || !vitureCamera.IsReady()) return;

        // CHECK 2: Ensure AI is ready
        if (yoloDetector == null || !yoloDetector.IsReady()) return;

        if (!isDetecting && Time.time - lastDetectionTime >= detectionInterval)
        {
            RunDetection();
            lastDetectionTime = Time.time;
        }
    }

    async void RunDetection()
    {
        isDetecting = true;

        // Use the new Low-Res capture to guarantee 640x640 input
        Texture2D frame = await vitureCamera.CaptureLowResFrameAsync();

        if (frame == null)
        {
            isDetecting = false;
            return;
        }

        if (showDebugLogs && currentFPS > 0)
            Debug.Log($"[YOLO] Scanning 640x640 frame... ({currentFPS:F1} FPS)");

        // Run the AI on the perfectly-sized frame
        currentDetections = yoloDetector.Detect(frame);

        // Clean up the temporary frame immediately to prevent memory leaks
        Destroy(frame);

        UpdateStatsDisplay();
        isDetecting = false;

        if (showDebugLogs && currentDetections.Count > 0)
        {
            Debug.Log($"[YOLO] SUCCESS! Detected {currentDetections.Count} objects.");
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
            statsText.text = $"FPS: {currentFPS:F1}\nDetections: {currentDetections.Count}";
    }
}