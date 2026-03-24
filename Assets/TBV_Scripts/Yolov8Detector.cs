using UnityEngine;
using Unity.Barracuda;
using System.Collections.Generic;
using System.Linq;

public class Yolov8Detector : MonoBehaviour
{
    public NNModel modelAsset;
    public int modelInputSize = 640;
    [Range(0f, 1f)] public float confidenceThreshold = 0.35f;
    [Range(0, 1)] public float nmsThreshold = 0.45f;
    public bool showDebugLogs = true;

    private IWorker worker;
    private RenderTexture resizedRT;
    private const int numBoxes = 8400;
    private const int numChannels = 6;
    private string[] labels = { "Rock", "Root" };

    void Start()
    {
        if (modelAsset == null) return;
        Model runtimeModel = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, runtimeModel);

        resizedRT = new RenderTexture(modelInputSize, modelInputSize, 0, RenderTextureFormat.ARGB32);
        resizedRT.Create();
    }

    public List<Detection> Detect(Texture frame)
    {
        if (worker == null || frame == null) return new List<Detection>();

        Graphics.Blit(frame, resizedRT);
        GL.Flush();

        using (var inputTensor = new Tensor(resizedRT, 3))
        {
            worker.Execute(inputTensor);

            using (var output = worker.CopyOutput())
            {
                return ParseOutput(output);
            }
        }
    }

    private float Sigmoid(float x)
    {
        if (x >= 0) return 1f / (1f + Mathf.Exp(-x));
        float z = Mathf.Exp(x);
        return z / (1f + z);
    }

    private List<Detection> ParseOutput(Tensor output)
    {
        List<Detection> candidates = new List<Detection>();

        // Use output.shape to determine how to loop
        // YOLOv8 Barracuda output is usually [1, 1, 8400, 6] or [1, 6, 8400, 1]
        int channels = output.shape.channels;
        int width = output.shape.width;
        int height = output.shape.height;

        // Check if classes are in the 'width' or 'channels' dimension
        bool isWidthClasses = (width == numChannels);

        for (int i = 0; i < numBoxes; i++)
        {
            float rockScore, rootScore, x, y, w, h;

            if (isWidthClasses)
            {
                // Format [1, 6, 8400, 1] - Accessing via (batch, height, width, channel)
                x = output[0, 0, i, 0];
                y = output[0, 1, i, 0];
                w = output[0, 2, i, 0];
                h = output[0, 3, i, 0];
                rockScore = output[0, 4, i, 0];
                rootScore = output[0, 5, i, 0];
            }
            else
            {
                // Format [1, 1, 8400, 6]
                x = output[0, 0, i, 0];
                y = output[0, 0, i, 1];
                w = output[0, 0, i, 2];
                h = output[0, 0, i, 3];
                rockScore = output[0, 0, i, 4];
                rootScore = output[0, 0, i, 5];
            }

            float highestScore = Mathf.Max(rockScore, rootScore);

            // Apply Sigmoid if the model outputs raw logits
            float confidence = highestScore;
            if (confidence > 1.5f || confidence < 0f)
            {
                confidence = 1f / (1f + Mathf.Exp(-confidence));
            }

            if (confidence > confidenceThreshold)
            {
                int classIdx = rockScore > rootScore ? 0 : 1;
                candidates.Add(new Detection
                {
                    classIndex = classIdx,
                    className = classIdx == 0 ? "Rock" : "Root",
                    confidence = confidence,
                    bbox = new Rect(x - w / 2f, y - h / 2f, w, h)
                });
            }
        }

        if (showDebugLogs)
            Debug.Log($"[YOLO] Found {candidates.Count} candidates. Running NMS...");

        return ApplyNMS(candidates, nmsThreshold);
    }

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
                if (IoU(current.bbox, sorted[i].bbox) > threshold)
                    sorted.RemoveAt(i);
            }
        }
        return selected;
    }

    private float IoU(Rect a, Rect b)
    {
        float areaA = a.width * a.height;
        float areaB = b.width * b.height;
        Rect intersect = Rect.MinMaxRect(
            Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin),
            Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
        if (intersect.width <= 0 || intersect.height <= 0) return 0;
        float intersectArea = intersect.width * intersect.height;
        return intersectArea / (areaA + areaB - intersectArea);
    }

    public bool IsReady() => worker != null;

    private void OnDisable()
    {
        worker?.Dispose();
        if (resizedRT != null) resizedRT.Release();
    }
}