using UnityEngine;
using UnityEngine.Android;

/// <summary>
/// Bridges to VitureImageBridge.java via Android Camera2.
/// Attach this to a GameObject named exactly "VitureNativeCamera" in the scene.
/// </summary>
public class VitureNativeCamera : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugLogs = true;

    // Must match the dimensions set in VitureImageBridge.java
    private const int FRAME_WIDTH = 640;
    private const int FRAME_HEIGHT = 480;

    private AndroidJavaClass imageBridge;
    private Texture2D latestFrame;
    private bool hasNewFrame = false;
    private bool hasReceivedFirstFrame = false;

    public bool IsReady { get; private set; } = false;

    void Start()
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            Debug.LogWarning("[NativeCamera] Not on Android, camera disabled.");
            return;
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            Invoke(nameof(InitBridge), 1.5f);
            return;
        }

        InitBridge();
    }

    void InitBridge()
    {
        try
        {
            // Initializing with the hardcoded 640x480 resolution
            latestFrame = new Texture2D(FRAME_WIDTH, FRAME_HEIGHT, TextureFormat.RGB24, false);
            imageBridge = new AndroidJavaClass("com.viture.barcode.VitureImageBridge");

            // CHANGED: Simplified signature to match the new Java start(String)
            bool ok = imageBridge.CallStatic<bool>("start", gameObject.name);

            if (ok)
            {
                IsReady = true;
                if (showDebugLogs) Debug.Log("[NativeCamera] Camera2 bridge started at 640x480.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NativeCamera] InitBridge failed: {e.Message}");
        }
    }

    // Called by Java via UnityPlayer.UnitySendMessage
    public void OnPreviewReady(string unused)
    {
        hasNewFrame = true;
        hasReceivedFirstFrame = true;
    }

    public Texture2D GetLatestFrame()
    {
        if (!IsReady || imageBridge == null || !hasReceivedFirstFrame) return null;

        // If no new frame has arrived from Java, return the last one we have
        if (!hasNewFrame) return latestFrame;

        try
        {
            // Pull the raw Y-plane (grayscale) bytes from Java
            byte[] rawBytes = imageBridge.CallStatic<byte[]>("getPreviewBytes");

            if (rawBytes != null && rawBytes.Length > 0)
            {
                // Directly load bytes into texture memory (No decompression needed!)
                latestFrame.LoadRawTextureData(rawBytes);
                latestFrame.Apply();

                hasNewFrame = false;
                return latestFrame;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NativeCamera] GetLatestFrame failed: {e.Message}");
        }

        return null;
    }

    void OnDestroy()
    {
        IsReady = false;
        if (imageBridge != null)
        {
            imageBridge.CallStatic("stop");
        }
        if (latestFrame != null)
        {
            Destroy(latestFrame);
        }
    }
}