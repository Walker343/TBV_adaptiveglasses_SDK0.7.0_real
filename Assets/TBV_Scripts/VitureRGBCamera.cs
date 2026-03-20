using UnityEngine;
using Viture.XR;
using UnityEngine.Rendering;
using System.Threading.Tasks;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class VitureRGBCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    public int desiredWidth = 640;
    public int desiredHeight = 640;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private RenderTexture cameraRenderTexture;
    private RenderTexture lowResRT;
    private bool isReady = false;

    void Start()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
        }
#endif
        Invoke(nameof(InitializeCamera), 2f);
    }

    void InitializeCamera()
    {
        if (!VitureXR.Camera.RGB.isSupported)
        {
            Debug.LogError("RGB camera is not supported on this device!");
            return;
        }

        var manager = VitureRGBCameraManager.Instance;
        if (manager != null)
        {
            cameraRenderTexture = manager.CameraRenderTexture;

            // Create the low-res GPU buffer (640x640)
            lowResRT = new RenderTexture(desiredWidth, desiredHeight, 0);
            lowResRT.Create();

            isReady = true;
            if (showDebugLogs) Debug.Log($"[VitureCamera] AI Buffer Initialized: {desiredWidth}x{desiredHeight}");
        }
    }

    /// <summary>
    /// Forces a low-resolution capture (640x640) asynchronously.
    /// This prevents the 1080p shape-mismatch crash.
    /// </summary>
    public async Task<Texture2D> CaptureLowResFrameAsync()
    {
        if (!isReady || cameraRenderTexture == null) return null;

        // 1. Resize from 1080p to 640x640 on the GPU
        Graphics.Blit(cameraRenderTexture, lowResRT);

        // 2. Request the data from the GPU asynchronously
        var request = AsyncGPUReadback.Request(lowResRT);

        while (!request.done)
        {
            await Task.Yield();
        }

        if (request.hasError) return null;

        // 3. Create a small texture and fill it with the 640x640 data
        Texture2D tex = new Texture2D(desiredWidth, desiredHeight, TextureFormat.RGBA32, false);
        tex.SetPixelData(request.GetData<Color32>(), 0);
        tex.Apply();

        return tex;
    }

    public bool IsReady()
    {
        return isReady && VitureXR.Camera.RGB.isActive && cameraRenderTexture != null;
    }
}