using UnityEngine;
using System.Collections.Generic;
using Bhaptics.SDK2;

public class HapticQuadrantManager : MonoBehaviour
{
    [Header("Components")]
    public VitureYOLOIntegration yoloIntegration;

    [Header("Haptic Event Keys")]
    [SerializeField] private string topLeftKey = "";  // Middle finger
    [SerializeField] private string topRightKey = "";  // Index finger
    [SerializeField] private string bottomLeftKey = "";  // Pinky finger
    [SerializeField] private string bottomRightKey = ""; // Ring finger
    [SerializeField] private string wristKey = "";  // Emergency wrist buzz

    [Header("Settings")]
    [Range(0.1f, 3f)]
    public float buzCooldown = 0.5f;
    [Range(0f, 1f)]
    public float intensity = 0.5f;
    [Range(0f, 1f)]
    public float duration = 0.3f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private const float SPLIT = 320f;
    private float[] lastBuzzTime = new float[4];
    private float lastWristBuzzTime = 0f;

    private const int TOP_LEFT = 0;
    private const int TOP_RIGHT = 1;
    private const int BOTTOM_LEFT = 2;
    private const int BOTTOM_RIGHT = 3;

    void Update()
    {
        if (yoloIntegration == null) return;

        List<Detection> detections = yoloIntegration.GetCurrentDetections();
        if (detections == null || detections.Count == 0) return;

        ProcessDetections(detections);
    }

    void ProcessDetections(List<Detection> detections)
    {
        bool[] quadrantHit = new bool[4];

        foreach (var det in detections)
        {
            float cx = det.bbox.x + det.bbox.width / 2f;
            float cy = det.bbox.y + det.bbox.height / 2f;
            quadrantHit[GetQuadrant(cx, cy)] = true;
        }

        bool allQuadrants = quadrantHit[0] && quadrantHit[1] && quadrantHit[2] && quadrantHit[3];

        if (allQuadrants)
        {
            TriggerWrist();
            if (showDebugLogs) Debug.Log("[Haptics] EMERGENCY - wrist buzz!");
            return;
        }

        for (int q = 0; q < 4; q++)
        {
            if (quadrantHit[q] && Time.time - lastBuzzTime[q] >= buzCooldown)
            {
                lastBuzzTime[q] = Time.time;
                TriggerFinger(q);
                if (showDebugLogs) Debug.Log($"[Haptics] Buzzing {GetQuadrantName(q)}");
            }
        }
    }

    int GetQuadrant(float cx, float cy)
    {
        bool isRight = cx >= SPLIT;
        bool isTop = cy < SPLIT;

        if (isTop && isRight) return TOP_RIGHT;
        if (isTop && !isRight) return TOP_LEFT;
        if (!isTop && isRight) return BOTTOM_RIGHT;
        return BOTTOM_LEFT;
    }

    void TriggerFinger(int quadrant)
    {
        string key = quadrant switch
        {
            TOP_LEFT => topLeftKey,
            TOP_RIGHT => topRightKey,
            BOTTOM_LEFT => bottomLeftKey,
            BOTTOM_RIGHT => bottomRightKey,
            _ => ""
        };

        if (!string.IsNullOrEmpty(key))
            BhapticsLibrary.Play(key);
    }

    void TriggerWrist()
    {
        if (Time.time - lastWristBuzzTime < buzCooldown) return;
        lastWristBuzzTime = Time.time;

        if (!string.IsNullOrEmpty(wristKey))
            BhapticsLibrary.Play(wristKey);
    }

    string GetQuadrantName(int q)
    {
        return q switch
        {
            TOP_LEFT => "TopLeft (Middle finger)",
            TOP_RIGHT => "TopRight (Index finger)",
            BOTTOM_LEFT => "BottomLeft (Pinky finger)",
            BOTTOM_RIGHT => "BottomRight (Ring finger)",
            _ => "Unknown"
        };
    }
}