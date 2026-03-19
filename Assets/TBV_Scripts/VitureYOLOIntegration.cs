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

    void Start()
    {
        Debug.Log("=== VitureYOLOIntegration START ===");

        if (vitureCamera == null)
            Debug.LogError("VitureRGBCamera component not assigned!");
        else
            Debug.Log("VitureRGBCamera assigned OK");

        if (yoloDetector == null)
            Debug.LogError("YOLOv8Detector component not assigned!");
        else
            Debug.Log("YOLOv8Detector assigned OK");
    }

    void Update()
    {
        UpdateFPS();

        if (yoloDetector == null || !yoloDetector.IsReady()) return;

        var manager = Viture.XR.VitureRGBCameraManager.Instance;
        if (manager == null || manager.CameraRenderTexture == null) return;

        if (!isDetecting && Time.time - lastDetectionTime >= detectionInterval)
        {
            lastDetectionTime = Time.time;
            RunDetection();
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

    async void RunDetection()
    {
        isDetecting = true;

        Texture2D frame = await Viture.XR.VitureRGBCameraManager.Instance.CaptureFrameAsync();

        if (frame == null)
        {
            Debug.LogWarning("[YOLO] CaptureFrameAsync returned null!");
            isDetecting = false;
            return;
        }

        if (showDebugLogs)
            Debug.Log($"[YOLO] Scanning frame... (Running at {currentFPS:F1} FPS)");

        currentDetections = yoloDetector.Detect(frame);
        Destroy(frame);

        UpdateStatsDisplay();

        if (showDebugLogs)
        {
            if (currentDetections.Count > 0)
            {
                Debug.Log($"[YOLO] SUCCESS! Detected {currentDetections.Count} objects:");
                foreach (var det in currentDetections)
                    Debug.Log($"  - {det.className} at ({det.bbox.x:F2}, {det.bbox.y:F2}) confidence: {det.confidence:F2}");
            }
            else
            {
                Debug.Log("[YOLO] Scan complete. 0 objects found.");
            }
        }

        isDetecting = false;
    }

    public List<Detection> GetCurrentDetections()
    {
        return new List<Detection>(currentDetections);
    }
}