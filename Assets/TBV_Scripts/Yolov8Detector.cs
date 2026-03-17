using System.Collections.Generic;
using Unity.Barracuda;
using UnityEngine;

// Moved outside the class to be accessible globally by your other scripts
[System.Serializable]
public struct Detection
{
    public int classIndex;
    public string className;
    public float confidence;
    public Rect bbox;
}

public class YOLOv8Detector : MonoBehaviour
{
    [Header("Model Files")]
    public NNModel modelAsset;
    private IWorker worker;

    [Header("Settings")]
    [Range(0, 1)] public float confidenceThreshold = 0.5f;
    [Range(0, 1)] public float iouThreshold = 0.45f;

    // Custom classes: Ensure these match your model's training order
    private string[] classNames = { "Rock", "Root" };

    void Awake()
    {
        if (modelAsset != null)
        {
            var model = ModelLoader.Load(modelAsset);
            worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, model);
        }
    }

    // Now a method to match your Manager script's "IsReady()" call
    public bool IsReady()
    {
        return worker != null;
    }

    public List<Detection> Detect(Texture2D inputTexture)
    {
        using (var inputTensor = new Tensor(inputTexture, 3))
        {
            worker.Execute(inputTensor);
            var output = worker.PeekOutput();

            List<Detection> results = PostProcess(output, inputTexture.width, inputTexture.height);

            output.Dispose();
            return results;
        }
    }

    private List<Detection> PostProcess(Tensor output, int width, int height)
    {
        List<Detection> allDetections = new List<Detection>();
        float[] data = output.ToReadOnlyArray();

        int numStructs = output.width; // 8400
        int numClasses = classNames.Length; // 2

        for (int i = 0; i < numStructs; i++)
        {
            float maxScore = 0f;
            int classId = -1;

            for (int c = 0; c < numClasses; c++)
            {
                // Indexing math for YOLOv8 (4 coords + class scores)
                float score = data[(4 + c) * numStructs + i];
                if (score > maxScore)
                {
                    maxScore = score;
                    classId = c;
                }
            }

            if (maxScore > confidenceThreshold)
            {
                float cx = data[0 * numStructs + i];
                float cy = data[1 * numStructs + i];
                float w = data[2 * numStructs + i];
                float h = data[3 * numStructs + i];

                float x = (cx - w / 2f) * width;
                float y = (cy - h / 2f) * height;
                float rectW = w * width;
                float rectH = h * height;

                allDetections.Add(new Detection
                {
                    classIndex = classId,
                    className = classNames[classId],
                    confidence = maxScore,
                    bbox = new Rect(x, y, rectW, rectH)
                });
            }
        }

        return ApplyNMS(allDetections);
    }

    private List<Detection> ApplyNMS(List<Detection> detections)
    {
        detections.Sort((a, b) => b.confidence.CompareTo(a.confidence));
        List<Detection> filtered = new List<Detection>();

        while (detections.Count > 0)
        {
            Detection top = detections[0];
            filtered.Add(top);
            detections.RemoveAt(0);

            for (int i = detections.Count - 1; i >= 0; i--)
            {
                if (CalculateIOU(top.bbox, detections[i].bbox) > iouThreshold)
                {
                    detections.RemoveAt(i);
                }
            }
        }
        return filtered;
    }

    private float CalculateIOU(Rect boxA, Rect boxB)
    {
        float areaA = boxA.width * boxA.height;
        float areaB = boxB.width * boxB.height;

        float x1 = Mathf.Max(boxA.xMin, boxB.xMin);
        float y1 = Mathf.Max(boxA.yMin, boxB.yMin);
        float x2 = Mathf.Min(boxA.xMax, boxB.xMax);
        float y2 = Mathf.Min(boxA.yMax, boxB.yMax);

        float interW = Mathf.Max(0, x2 - x1);
        float interH = Mathf.Max(0, y2 - y1);
        float interArea = interW * interH;

        return interArea / (areaA + areaB - interArea);
    }

    private void OnDestroy()
    {
        worker?.Dispose();
    }
}