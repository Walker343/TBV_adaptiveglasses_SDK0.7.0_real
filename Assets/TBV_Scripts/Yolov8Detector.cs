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

    private const int numBoxes = 8400;
    private const int numChannels = 6;

    void Start()
    {
        if (modelAsset == null)
        {
            Debug.LogError("[YOLO] ERROR: Neural Network Model Asset is MISSING in the Inspector!");
            return;
        }

        Debug.Log("[YOLO] Loading model...");
        Model runtimeModel = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, runtimeModel);
        Debug.Log("[YOLO] Worker initialized successfully! Ready for inference.");
    }

    private float Sigmoid(float x) => 1.0f / (1.0f + Mathf.Exp(-x));

    public List<Detection> Detect(Texture2D frame)
    {
        List<Detection> results = new List<Detection>();
        if (worker == null || frame == null) return results;

        if (resizedRT == null)
        {
            resizedRT = new RenderTexture(modelInputSize, modelInputSize, 0, RenderTextureFormat.ARGB32);
            resizedRT.Create();
        }

        Graphics.Blit(frame, resizedRT);

        using (var inputTensor = new Tensor(resizedRT, 3))
        {
            worker.Execute(inputTensor);
            var output = worker.CopyOutput();
            List<Detection> candidates = new List<Detection>();

            // Labels must match your training order
            string[] labels = { "Rock", "Root" };

            for (int i = 0; i < numBoxes; i++)
            {
                // Fix: Barracuda Tensors require 4 indices [batch, height, width, channel]
                // For YOLOv8 outputs [1, 6, 8400], it maps to [0, 0, channelIndex, boxIndex]
                float x_center = output[0, 0, 0, i];
                float y_center = output[0, 0, 1, i];
                float w = output[0, 0, 2, i];
                float h = output[0, 0, 3, i];

                // Find the best class score starting from index 4
                float maxClassScore = -Mathf.Infinity;
                int classId = 0;

                for (int c = 0; c < (numChannels - 4); c++)
                {
                    float score = output[0, 0, 4 + c, i];
                    if (score > maxClassScore)
                    {
                        maxClassScore = score;
                        classId = c;
                    }
                }

                // Convert raw logit to a 0.0-1.0 probability
                float confidence = Sigmoid(maxClassScore);

                if (confidence > confidenceThreshold)
                {
                    // Map center-coordinates to Top-Left Rect format
                    float x = x_center - (w / 2f);
                    float y = y_center - (h / 2f);

                    candidates.Add(new Detection
                    {
                        classIndex = classId,
                        className = labels[classId],
                        confidence = confidence,
                        bbox = new Rect(x, y, w, h)
                    });
                }
            }

            // 4. Remove overlapping boxes
            results = ApplyNMS(candidates, nmsThreshold);
        }

        return results;
    }

    public bool IsReady() => worker != null;

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
                if (IntersectionOverUnion(current.bbox, sorted[i].bbox) > threshold) sorted.RemoveAt(i);
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
        float intersectArea = intersect.width * intersect.height;
        return intersectArea / (areaA + areaB - intersectArea);
    }

    private void OnDisable()
    {
        worker?.Dispose();
        if (resizedRT != null) resizedRT.Release();
    }
}