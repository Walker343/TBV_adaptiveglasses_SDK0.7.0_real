using UnityEngine;

[System.Serializable]
public struct Detection
{
    public int classIndex;
    public string className;
    public float confidence;
    public Rect bbox;
}