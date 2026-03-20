using UnityEngine;
using Unity.Barracuda;
using System.Collections.Generic;
using System.Linq;

public class Yolov8Detector : MonoBehaviour
{
    [Header("Resources")]
    public NNModel modelAsset;

    [Header("Settings")]
    public int modelInputSize = 640;
    [Range(0, 1)] public float confidenceThreshold = 0.5f;
    [Range(0, 1)] public float nmsThreshold = 0.45f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private IWorker worker;
    private RenderTexture resizedRT;

    // Standard YOLOv8-base constants
    private const int numBoxes = 8400;
    private const int numChannels = 6;

    void Start()
    {
        if (modelAsset == null)
        {
            Debug.LogError("[YOLO] ERROR: Model Asset is MISSING!");
            return;
        }

        // 1. Load the model
        Model runtimeModel = ModelLoader.Load(modelAsset);

        // 2. Create the worker (GPU-accelerated)
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, runtimeModel);

        // 3. Initialize the inference buffer immediately.
        // Doing this here prevents the "Off-axis" crash caused by uninitialized textures.
        resizedRT = new RenderTexture(modelInputSize, modelInputSize, 0, RenderTextureFormat.ARGB32);
        resizedRT.Create();

        if (showDebugLogs)
            Debug.Log($"[YOLO] Worker initialized. Buffer set to {modelInputSize}x{modelInputSize}.");
    }

    public List<Detection> Detect(Texture2D frame)
    {
        if (worker == null || frame == null) return new List<Detection>();

        // 4. Use the GPU to force the input frame into the perfect 640x640 shape
        Graphics.Blit(frame, resizedRT);

        // 5. Convert to Tensor for Barracuda
        using (var inputTensor = new Tensor(resizedRT, 3))
        {
            worker.Execute(inputTensor);

            // Get output and process
            var output = worker.PeekOutput();
            var results = ParseOutput(output);

            return results;
        }
    }

    private List<Detection> ParseOutput(Tensor output)
    {
        List<Detection> candidates = new List<Detection>();

        // YOLOv8 output is usually interpreted by Barracuda as:
        // batch: 0, height: 0, width: channel_index, channels: box_index
        // Or sometimes: [0, 0, box_index, channel_index]

        for (int i = 0; i < numBoxes; i++)
        {
            // Try the [batch, height, width, channel] format
            // In your case, this usually maps to [0, 0, 4, i] for confidence
            float confidence = output[0, 0, i, 4];

            if (confidence > confidenceThreshold)
            {
                float x = output[0, 0, i, 0];
                float y = output[0, 0, i, 1];
                float w = output[0, 0, i, 2];
                float h = output[0, 0, i, 3];
                int classIdx = Mathf.RoundToInt(output[0, 0, i, 5]);

                candidates.Add(new Detection
                {
                    classIndex = classIdx,
                    className = "Object",
                    confidence = confidence,
                    bbox = new Rect(x - w / 2, y - h / 2, w, h)
                });
            }
        }

        return ApplyNMS(candidates, nmsThreshold);
    }

    public bool IsReady() => worker != null && resizedRT != null;

    private List<Detection> ApplyNMS(List<Detection> boxes, float threshold)
    {
        var sorted = boxes.OrderByDescending(b => b.confidence).ToList();
        List<Detection> selected = new List<Detection>();

        while (sorted.Count > 0)
        {
            var current = sorted[0];
            selected.Add(current);
            sorted.RemoveAt(0);

            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                if (IntersectionOverUnion(current.bbox, sorted[i].bbox) > threshold)
                    sorted.RemoveAt(i);
            }
        }
        return selected;
    }

    private float IntersectionOverUnion(Rect a, Rect b)
    {
        float areaA = a.width * a.height;
        float areaB = b.width * b.height;
        Rect intersect = Rect.MinMaxRect(
            Mathf.Max(a.xMin, b.xMin),
            Mathf.Max(a.yMin, b.yMin),
            Mathf.Min(a.xMax, b.xMax),
            Mathf.Min(a.yMax, b.yMax)
        );

        if (intersect.width <= 0 || intersect.height <= 0) return 0;
        float intersectionArea = intersect.width * intersect.height;
        return intersectionArea / (areaA + areaB - intersectionArea);
    }

    private void OnDestroy()
    {
        // Clean up GPU resources
        worker?.Dispose();
        if (resizedRT != null) resizedRT.Release();
    }
}