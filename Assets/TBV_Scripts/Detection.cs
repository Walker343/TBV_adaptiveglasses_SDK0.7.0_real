using UnityEngine;

[System.Serializable]
public struct Detection
{
    public int classIndex;
    public string className;
    public float confidence;
    public Rect bbox;

    public override string ToString()
    {
        return $"{className} ({confidence:F2}) at {bbox}";
    }
}