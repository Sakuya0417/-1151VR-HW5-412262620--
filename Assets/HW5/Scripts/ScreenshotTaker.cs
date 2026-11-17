using System.IO;
using UnityEngine;

/// <summary>執行時按 F12（或 P）截圖，存到專案根目錄的 Screenshots 資料夾，可直接放進 README。</summary>
public class ScreenshotTaker : MonoBehaviour
{
    public string folder = "Screenshots";
    public int superSize = 1;

    void Update()
    {
        if (HW5Input.GetKeyDown(KeyCode.F12) || HW5Input.GetKeyDown(KeyCode.P))
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "HW5_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(path, superSize);
            Debug.Log("[HW5] 截圖已儲存：" + Path.GetFullPath(path));
        }
    }
}
