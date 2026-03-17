using UnityEngine;
using Viture.XR;
#if UNITY_ANDROID
using UnityEngine.Android; // This is the line that was missing or being ignored
#endif

public class VitureRGBCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    public int desiredWidth = 1920;
    public int desiredHeight = 1080;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private RenderTexture cameraRenderTexture;
    private Texture2D currentFrame;
    private bool isReady = false;

    // We'll use this to prevent spamming the console if it's not ready yet
    private float nextDebugLogTime = 0f;

    void Start()
    {
        // This 'if' block tells Unity: "Only look at this code if we are building for Android"
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.Log("[VitureCamera] Requesting Camera Permission from OS...");
            Permission.RequestUserPermission(Permission.Camera);
        }
#endif

        // Wait 2 seconds to give the user time to see the Android popup
        Invoke(nameof(InitializeCamera), 2f);
    }

    void InitializeCamera()
    {
        if (!VitureXR.Camera.RGB.isSupported)
        {
            Debug.LogError("RGB camera is not supported on this device!");
            return;
        }

        if (VitureRGBCameraManager.Instance == null)
        {
            Debug.LogError("VitureRGBCameraManager not found in scene! Make sure it exists.");
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log("RGB camera is supported! Forcing Start...");
        }

        // Your exact logic to force the camera to start
        VitureXR.Camera.RGB.Start();

        isReady = true;
        Debug.Log("[VitureCamera] Automated Startup Complete. Camera Start() command sent.");
    }

    void Update()
    {
        if (!isReady) return;

        VitureRGBCameraManager manager = VitureRGBCameraManager.Instance;
        if (manager != null && manager.CameraRenderTexture != null)
        {
            RenderTexture rt = manager.CameraRenderTexture;

            if (currentFrame == null || rt != cameraRenderTexture)
            {
                cameraRenderTexture = rt;

                // Recreate Texture2D if resolution changed or it doesn't exist yet
                if (currentFrame == null || currentFrame.width != rt.width || currentFrame.height != rt.height)
                {
                    if (currentFrame != null)
                        Destroy(currentFrame);

                    currentFrame = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    Debug.Log($"Created Texture2D for YOLO: {rt.width}x{rt.height}");
                }
            }

            // Copy RenderTexture to Texture2D for YOLO
            if (cameraRenderTexture != null && currentFrame != null)
            {
                RenderTexture.active = cameraRenderTexture;
                currentFrame.ReadPixels(new Rect(0, 0, cameraRenderTexture.width, cameraRenderTexture.height), 0, 0);
                currentFrame.Apply();
                RenderTexture.active = null;
            }
        }
    }

    public Texture2D GetCurrentFrame()
    {
        return currentFrame;
    }

    public bool IsReady()
    {
        bool isActive = VitureXR.Camera.RGB.isActive;
        bool hasFrame = currentFrame != null;
        bool hasManager = VitureRGBCameraManager.Instance != null;
        bool hasRT = hasManager && VitureRGBCameraManager.Instance.CameraRenderTexture != null;

        bool readyStatus = isReady && isActive && hasFrame && hasManager && hasRT;

        // If it's NOT ready, print a log every 2 seconds explaining exactly WHY
        if (!readyStatus && showDebugLogs && Time.time > nextDebugLogTime && isReady)
        {
            Debug.LogWarning($"[Camera Waiting] isActive:{isActive}, hasFrame:{hasFrame}, hasManager:{hasManager}, hasRT:{hasRT}");
            nextDebugLogTime = Time.time + 2f;
        }

        return readyStatus;
    }

    void OnDestroy()
    {
        // Make sure we shut the camera down properly
        if (VitureXR.Camera.RGB.isSupported)
        {
            VitureXR.Camera.RGB.Stop();
        }
    }
}