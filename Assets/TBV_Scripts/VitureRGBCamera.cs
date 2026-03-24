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

    [Header("Gamma Correction")]
    [Range(0.1f, 4.0f)]
    public float gammaCorrection = 0.6f; // <1 darkens bright scenes, >1 brightens dark scenes

    [Header("Debug")]
    public bool showDebugLogs = true;

    private RenderTexture lowResRT;
    private Texture2D latestFrame;
    private Material oesMaterial;
    private bool isReady = false;

    private const string k_OESBlitShader = "Hidden/VitureXR/OESBlit";

    void Start()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            Permission.RequestUserPermission(Permission.Camera);
#endif
        Invoke(nameof(InitializeCamera), 2f);
    }

    void InitializeCamera()
    {
        if (!VitureXR.Camera.RGB.isSupported)
        {
            Debug.LogError("[VitureRGBCamera] RGB camera not supported!");
            return;
        }

        var resolutions = VitureXR.Camera.RGB.GetSupportedResolutions();
        foreach (var res in resolutions)
            Debug.Log($"[Camera] Supported resolution: {res.x}x{res.y}");

        VitureXR.Camera.RGB.Start();

        Shader shader = Shader.Find(k_OESBlitShader);
        if (shader != null)
        {
            oesMaterial = new Material(shader);
            if (showDebugLogs) Debug.Log("[VitureRGBCamera] OES Shader loaded.");
        }
        else
        {
            Debug.LogWarning("[VitureRGBCamera] OES Shader not found, falling back.");
        }

        lowResRT = new RenderTexture(desiredWidth, desiredHeight, 0, RenderTextureFormat.ARGB32);
        lowResRT.Create();

        latestFrame = new Texture2D(desiredWidth, desiredHeight, TextureFormat.RGBA32, false);

        isReady = true;
        if (showDebugLogs) Debug.Log($"[VitureRGBCamera] Ready: {desiredWidth}x{desiredHeight}");
    }

    public bool IsReady()
    {
        var manager = Viture.XR.VitureRGBCameraManager.Instance;
        bool managerOk = manager != null;
        bool textureOk = managerOk && manager.CameraRenderTexture != null;
        bool activeOk = VitureXR.Camera.RGB.isActive;
        if (showDebugLogs)
            Debug.Log($"[VitureRGBCamera] IsReady check — ready:{isReady} manager:{managerOk} texture:{textureOk} active:{activeOk}");
        return isReady && textureOk && activeOk;
    }

    public async Task<Texture2D> CaptureLowResFrameAsync()
    {
        var manager = Viture.XR.VitureRGBCameraManager.Instance;
        if (!isReady || manager == null || manager.CameraRenderTexture == null) return null;

        RenderTexture liveRT = manager.CameraRenderTexture;

        if (oesMaterial != null)
            Graphics.Blit(liveRT, lowResRT, oesMaterial);
        else
            Graphics.Blit(liveRT, lowResRT);

        var request = AsyncGPUReadback.Request(lowResRT);
        while (!request.done) await Task.Yield();

        if (request.hasError)
        {
            if (showDebugLogs) Debug.LogError("[VitureRGBCamera] GPU Readback Error.");
            return null;
        }

        latestFrame.SetPixelData(request.GetData<byte>(), 0);
        latestFrame.Apply();

        // Gamma correction - tune gammaCorrection in Inspector
        // 0.4 = darken bright outdoor, 0.6 = indoor daylight, 2.5 = dark indoor
        Color32[] pixels = latestFrame.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i].r = (byte)(Mathf.Pow(pixels[i].r / 255f, gammaCorrection) * 255f);
            pixels[i].g = (byte)(Mathf.Pow(pixels[i].g / 255f, gammaCorrection) * 255f);
            pixels[i].b = (byte)(Mathf.Pow(pixels[i].b / 255f, gammaCorrection) * 255f);
        }
        latestFrame.SetPixels32(pixels);
        latestFrame.Apply();

        return latestFrame;
    }

    private void OnDestroy()
    {
        if (lowResRT != null) lowResRT.Release();
        if (latestFrame != null) Destroy(latestFrame);
        if (oesMaterial != null) Destroy(oesMaterial);
    }
}
