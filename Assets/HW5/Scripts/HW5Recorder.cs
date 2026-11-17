using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 錄製一次完整演出（從開始執行到收服成功），輸出到專案根目錄的 Recordings/：
///   HW5_frames.mjpeg ── 固定 30 fps 的連續 JPG 畫面（Time.captureFramerate 確保不掉格）
///   HW5_audio.wav    ── 依實際播放時間把音效混成的音軌
/// 再用 ffmpeg 合成 MP4：
///   ffmpeg -framerate 30 -f mjpeg -i HW5_frames.mjpeg -i HW5_audio.wav -c:v libx264 -pix_fmt yuv420p -c:a aac HW5.mp4
/// 由選單 HW5 ▸ Record Video 啟動。
/// </summary>
public class HW5Recorder : MonoBehaviour
{
    public static bool RequestRecording;

    public int fps = 30;
    public float tailSeconds = 4f;
    public string folder = "Recordings";

    struct Sfx { public AudioClip clip; public float time; public float vol; }
    readonly List<Sfx> events = new List<Sfx>();
    PokeballThrowController ctrl;
    int frames;

    void Start()
    {
#if UNITY_EDITOR
        RequestRecording = UnityEditor.SessionState.GetBool("HW5_Record", false);
        UnityEditor.SessionState.SetBool("HW5_Record", false);
#endif
        if (!RequestRecording) { enabled = false; return; }
        ctrl = GetComponent<PokeballThrowController>();
        PokeballThrowController.SfxPlayed += OnSfx;
        Time.captureFramerate = fps;
        StartCoroutine(Record());
    }

    void OnDestroy() { PokeballThrowController.SfxPlayed -= OnSfx; }

    void OnSfx(AudioClip clip, float vol)
    {
        events.Add(new Sfx { clip = clip, time = frames / (float)fps, vol = vol });
    }

    IEnumerator Record()
    {
        Directory.CreateDirectory(folder);
        string videoPath = Path.Combine(folder, "HW5_frames.mjpeg");
        float endAt = float.MaxValue;
        using (var fs = new FileStream(videoPath, FileMode.Create))
        {
            while (frames / (float)fps < endAt && frames < fps * 60)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] jpg = tex.EncodeToJPG(92);
                Destroy(tex);
                fs.Write(jpg, 0, jpg.Length);
                frames++;
                if (endAt == float.MaxValue && ctrl != null && ctrl.IsDone)
                    endAt = frames / (float)fps + tailSeconds;
            }
        }
        WriteWav(Path.Combine(folder, "HW5_audio.wav"), frames / (float)fps);
        Time.captureFramerate = 0;
        Debug.Log("[HW5] 錄影完成：" + Path.GetFullPath(folder) + "（" + frames + " 格）");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void WriteWav(string path, float seconds)
    {
        const int sr = 44100;
        int n = Mathf.CeilToInt(seconds * sr);
        var mix = new float[n];
        foreach (var e in events)
        {
            if (e.clip == null) continue;
            var data = new float[e.clip.samples * e.clip.channels];
            e.clip.GetData(data, 0);
            int start = Mathf.RoundToInt(e.time * sr);
            for (int i = 0; i < e.clip.samples && start + i < n; i++)
                mix[start + i] += data[i * e.clip.channels] * e.vol;
        }
        using (var bw = new BinaryWriter(new FileStream(path, FileMode.Create)))
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + n * 2);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(n * 2);
            for (int i = 0; i < n; i++)
                bw.Write((short)(Mathf.Clamp(mix[i], -1f, 1f) * 32000f));
        }
    }
}
