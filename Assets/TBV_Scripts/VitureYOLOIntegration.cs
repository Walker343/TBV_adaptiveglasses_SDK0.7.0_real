using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class VitureYOLOIntegration : MonoBehaviour
{
    public VitureRGBCamera vitureCamera;
    public Yolov8Detector yoloDetector;

    [Header("Settings")]
    public float detectionInterval = 0.5f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool isProcessing = false;
    private List<Detection> currentDetections = new List<Detection>();

    private void Start()
    {
        InvokeRepeating(nameof(TriggerDetection), 2f, detectionInterval);
    }

    private void TriggerDetection()
    {
        if (isProcessing) return;
        _ = RunDetection();
    }

    public List<Detection> GetCurrentDetections()
    {
        return new List<Detection>(currentDetections);
    }

    private async Task RunDetection()
    {
        if (vitureCamera == null || !vitureCamera.IsReady()) return;

        isProcessing = true;

        try
        {
            Texture2D frame = await vitureCamera.CaptureLowResFrameAsync();

            if (frame != null)
            {
                if (showDebugLogs)
                {
                    Color centerPixel = frame.GetPixel(320, 320);
                    Debug.Log($"[YOLO Probe] Center Pixel: {centerPixel} (R:{centerPixel.r:F2})");
                }

                try
                {
                    byte[] bytes = frame.EncodeToJPG();
                    string path = Application.persistentDataPath + "/yolo_debug.jpg";
                    System.IO.File.WriteAllBytes(path, bytes);
                    if (showDebugLogs) Debug.Log($"[YOLO Image] Saved to: {path}");
                }
                catch (System.Exception imgError)
                {
                    Debug.Log($"[YOLO Image Error] {imgError.Message}");
                }

                currentDetections = yoloDetector.Detect(frame);

                if (showDebugLogs)
                {
                    if (currentDetections.Count > 0)
                    {
                        Debug.Log($"[YOLO] Detected {currentDetections.Count} objects:");
                        foreach (var det in currentDetections)
                            Debug.Log($"  - {det.className} at ({det.bbox.x:F0},{det.bbox.y:F0}) conf:{det.confidence:F2}");
                    }
                    else
                    {
                        Debug.Log("[YOLO] No detections.");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VitureYOLO] Detection Error: {e.Message}");
        }
        finally
        {
            isProcessing = false;
        }
    }
}