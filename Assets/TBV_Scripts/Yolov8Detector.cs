using UnityEngine;
using Unity.Barracuda;
using System.Collections.Generic;

// Note: Ensure your Detection.cs file defines Detection as a 'struct' to reduce memory pressure
public class YOLOv8Detector : MonoBehaviour
{
    [Header("Model Settings")]
    public NNModel modelAsset;

    [Header("Detection Settings")]
    [Range(0f, 1f)] public float confidenceThreshold = 0.25f;
    [Range(0f, 1f)] public float iouThreshold = 0.45f;

    [Header("Input Settings")]
    public int inputWidth = 640;
    public int inputHeight = 640;

    private Model runtimeModel;
    private IWorker engine;
    private bool isInitialized = false;

    // Updated for your specific model classes
    private string[] classNames = new string[] { "Rock", "Root" };

    void Start()
    {
        Invoke(nameof(InitializeModel), 2f);
    }

    void InitializeModel()
    {
        if (modelAsset == null)
        {
            Debug.LogError("No model asset assigned!");
            return;
        }

        try
        {
            runtimeModel = ModelLoader.Load(modelAsset);
            engine = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, runtimeModel);
            isInitialized = true;
            Debug.Log("YOLOv8 custom model (Rock/Root) initialized successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize model: {e.Message}");
        }
    }

    public List<Detection> Detect(Texture2D inputImage)
    {
        if (!isInitialized) return new List<Detection>();

        Tensor inputTensor = Preprocess(inputImage);
        engine.Execute(inputTensor);
        Tensor output = engine.PeekOutput();
        List<Detection> detections = PostProcess(output, inputImage.width, inputImage.height);
        inputTensor.Dispose();

        return detections;
    }

    Tensor Preprocess(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(inputWidth, inputHeight, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        Tensor tensor = new Tensor(rt, 3);
        RenderTexture.ReleaseTemporary(rt);
        return tensor;
    }

    List<Detection> PostProcess(Tensor output, int originalWidth, int originalHeight)
    {
        List<Detection> allDetections = new List<Detection>();
        float[] outputData = output.ToReadOnlyArray();

        int numPredictions = 8400;
        int numClasses = classNames.Length; // Adjusted to 2 classes

        for (int i = 0; i < numPredictions; i++)
        {
            float maxConfidence = 0f;
            int maxClassIndex = 0;

            for (int c = 0; c < numClasses; c++)
            {
                // Indexing logic for YOLOv8: 4 box coords + N classes
                int index = (4 + c) * numPredictions + i;
                float confidence = outputData[index];

                if (confidence > maxConfidence)
                {
                    maxConfidence = confidence;
                    maxClassIndex = c;
                }
            }

            if (maxConfidence < confidenceThreshold) continue;

            float cx = outputData[0 * numPredictions + i];
            float cy = outputData[1 * numPredictions + i];
            float w = outputData[2 * numPredictions + i];
            float h = outputData[3 * numPredictions + i];

            float x1 = (cx - w / 2f) * originalWidth;
            float y1 = (cy - h / 2f) * originalHeight;
            float boxWidth = w * originalWidth;
            float boxHeight = h * originalHeight;

            allDetections.Add(new Detection
            {
                classIndex = maxClassIndex,
                className = classNames[maxClassIndex],
                confidence = maxConfidence,
                bbox = new Rect(x1, y1, boxWidth, boxHeight)
            });
        }

        return ApplyNMS(allDetections, iouThreshold);
    }

    List<Detection> ApplyNMS(List<Detection> detections, float iouThreshold)
    {
        detections.Sort((a, b) => b.confidence.CompareTo(a.confidence));
        List<Detection> result = new List<Detection>();
        bool[] suppressed = new bool[detections.Count];

        for (int i = 0; i < detections.Count; i++)
        {
            if (suppressed[i]) continue;
            result.Add(detections[i]);

            for (int j = i + 1; j < detections.Count; j++)
            {
                if (suppressed[j]) continue;
                if (CalculateIOU(detections[i].bbox, detections[j].bbox) > iouThreshold)
                {
                    suppressed[j] = true;
                }
            }
        }

        return result;
    }

    float CalculateIOU(Rect box1, Rect box2)
    {
        float x1 = Mathf.Max(box1.xMin, box2.xMin);
        float y1 = Mathf.Max(box1.yMin, box2.yMin);
        float x2 = Mathf.Min(box1.xMax, box2.xMax);
        float y2 = Mathf.Min(box1.yMax, box2.yMax);

        float intersectionArea = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
        float unionArea = box1.width * box1.height + box2.width * box2.height - intersectionArea;

        return unionArea == 0 ? 0 : intersectionArea / unionArea;
    }

    public bool IsReady()
    {
        return isInitialized;
    }

    void OnDestroy()
    {
        // Fix for "Low Memory on Close": Explicitly release resources
        if (engine != null)
        {
            engine.Dispose();
            engine = null;
        }

        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }
}