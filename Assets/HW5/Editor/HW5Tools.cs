using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>HW5 選單的輔助工具：未執行前的場景截圖、錄製演出影片。</summary>
public static class HW5Tools
{
    [MenuItem("HW5/Capture Preview Screenshot", priority = 20)]
    public static void CapturePreview()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[HW5] 找不到 Main Camera"); return; }
        const int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = prev;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Directory.CreateDirectory("Screenshots");
        string path = Path.GetFullPath("Screenshots/HW5_preview.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("[HW5] 未執行前的場景截圖：" + path);
    }

    [MenuItem("HW5/Record Video", priority = 21)]
    public static void RecordVideo()
    {
        if (EditorApplication.isPlaying) return;
        EditorSceneManager.SaveOpenScenes();
        var gm = GameObject.Find("GameManager");
        if (gm != null && gm.GetComponent<HW5Recorder>() == null)
        {
            gm.AddComponent<HW5Recorder>();
            EditorSceneManager.MarkSceneDirty(gm.scene);
            EditorSceneManager.SaveOpenScenes();
        }
        SessionState.SetBool("HW5_Record", true);
        EditorApplication.isPlaying = true;
    }
}
