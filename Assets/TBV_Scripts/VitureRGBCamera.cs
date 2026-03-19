using UnityEngine;
using Viture.XR;
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
    private RenderTexture lowResRT; // The 640x640 GPU buffer
    private Texture2D currentFrame;
    private bool isReady = false;
    private float nextDebugLogTime = 0f;

    void Start()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.Log("[VitureCamera] Requesting Camera Permission...");
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

            // 1. Initialize the Texture2D at the SMALLER size
            currentFrame = new Texture2D(desiredWidth, desiredHeight, TextureFormat.RGB24, false);

            // 2. Create the low-res GPU buffer
            lowResRT = new RenderTexture(desiredWidth, desiredHeight, 0);
            lowResRT.Create();

            isReady = true;
            Debug.Log($"[VitureCamera] Initialized. Output size: {desiredWidth}x{desiredHeight}");
        }
    }

    void Update()
    {
        if (isReady && cameraRenderTexture != null)
        {
            // 3. Blit (Copy + Resize) from 1080p to 640x640 on the GPU
            Graphics.Blit(cameraRenderTexture, lowResRT);

            // 4. Read ONLY the 640x640 pixels into the Texture2D
            RenderTexture.active = lowResRT;
            currentFrame.ReadPixels(new Rect(0, 0, desiredWidth, desiredHeight), 0, 0);
            currentFrame.Apply();
            RenderTexture.active = null;
        }
    }

    public Texture2D GetCurrentFrame() => currentFrame;

    public bool IsReady()
    {
        bool isActive = VitureXR.Camera.RGB.isActive;
        bool readyStatus = isReady && isActive && currentFrame != null;

        if (!readyStatus && showDebugLogs && Time.time > nextDebugLogTime && isReady)
        {
            Debug.LogWarning($"[Camera Waiting] isActive:{isActive}");
            nextDebugLogTime = Time.time + 2f;
        }

        return readyStatus;
    }

    void OnDestroy()
    {
        if (lowResRT != null) lowResRT.Release();
        if (currentFrame != null) Destroy(currentFrame);
    }
}