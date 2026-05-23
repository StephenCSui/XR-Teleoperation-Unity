using UnityEngine;
using UnityEngine.UI;

public class RealSenseWebCamFeed : MonoBehaviour
{
    [Header("Target UI")]
    [SerializeField] private RawImage targetRawImage;

    [Header("Camera Selection")]
    [SerializeField] private string preferredDeviceKeyword = "RealSense";
    [SerializeField] private int fallbackDeviceIndex = 0;

    [Header("Stream Settings")]
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 480;
    [SerializeField] private int requestedFPS = 30;

    [Header("Display Correction")]
    [SerializeField] private bool mirrorHorizontally = false;
    [SerializeField] private bool flipVertically = false;

    private WebCamTexture webCamTexture;

    private void Start()
    {
        StartCameraFeed();
    }

    private void StartCameraFeed()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        Debug.Log($"[RealSenseWebCamFeed] Camera devices found: {devices.Length}");

        if (devices.Length == 0)
        {
            Debug.LogError("[RealSenseWebCamFeed] No camera devices found. Check USB connection, Windows camera permission, and close RealSense Viewer.");
            return;
        }

        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"[RealSenseWebCamFeed] Device [{i}]: {devices[i].name}");
        }

        string selectedDeviceName = SelectCameraDevice(devices);

        Debug.Log($"[RealSenseWebCamFeed] Selected camera: {selectedDeviceName}");

        webCamTexture = new WebCamTexture(
            selectedDeviceName,
            requestedWidth,
            requestedHeight,
            requestedFPS
        );

        if (targetRawImage == null)
        {
            Debug.LogError("[RealSenseWebCamFeed] Target RawImage is not assigned.");
            return;
        }

        targetRawImage.texture = webCamTexture;
        targetRawImage.color = Color.white;

        ApplyDisplayCorrection();

        webCamTexture.Play();

        Debug.Log("[RealSenseWebCamFeed] Camera feed started.");
    }

    private string SelectCameraDevice(WebCamDevice[] devices)
    {
        if (!string.IsNullOrWhiteSpace(preferredDeviceKeyword))
        {
            string keywordLower = preferredDeviceKeyword.ToLower();

            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].name.ToLower().Contains(keywordLower))
                {
                    return devices[i].name;
                }
            }

            Debug.LogWarning($"[RealSenseWebCamFeed] No device matched keyword: {preferredDeviceKeyword}");
        }

        if (fallbackDeviceIndex >= 0 && fallbackDeviceIndex < devices.Length)
        {
            Debug.LogWarning($"[RealSenseWebCamFeed] Falling back to device index: {fallbackDeviceIndex}");
            return devices[fallbackDeviceIndex].name;
        }

        Debug.LogWarning("[RealSenseWebCamFeed] Invalid fallback index. Using device 0.");
        return devices[0].name;
    }

    private void ApplyDisplayCorrection()
    {
        if (targetRawImage == null)
        {
            return;
        }

        Vector3 scale = targetRawImage.rectTransform.localScale;

        scale.x = Mathf.Abs(scale.x) * (mirrorHorizontally ? -1f : 1f);
        scale.y = Mathf.Abs(scale.y) * (flipVertically ? -1f : 1f);

        targetRawImage.rectTransform.localScale = scale;
    }

    private void Update()
    {
        if (webCamTexture == null)
        {
            return;
        }

        if (webCamTexture.isPlaying && webCamTexture.width > 100)
        {
            // This confirms the stream is actually producing frames.
            // Kept lightweight so it does not spam every frame.
        }
    }

    private void OnDestroy()
    {
        StopCameraFeed();
    }

    private void OnApplicationQuit()
    {
        StopCameraFeed();
    }

    private void StopCameraFeed()
    {
        if (webCamTexture != null)
        {
            if (webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }

            webCamTexture = null;
            Debug.Log("[RealSenseWebCamFeed] Camera feed stopped.");
        }
    }
}