using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PropellerAudio : MonoBehaviour
{
    public DroneController droneController;

    AudioSource audioSource;
    const int sampleRate = 44100;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = GenerateClip();
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0f;
        audioSource.pitch = 0.8f;
        audioSource.Play();
    }

    void Start()
    {
        if (droneController == null)
            droneController = GetComponent<DroneController>();
    }

    void Update()
    {
        if (droneController == null) return;
        float throttle = droneController.Throttle;

        float targetVolume = throttle > 0f ? Mathf.Lerp(0.02f, 0.05f, throttle) : 0f;
        float targetPitch  = Mathf.Lerp(0.7f, 1.5f, throttle);

        audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * 4f);
        audioSource.pitch  = Mathf.Lerp(audioSource.pitch,  targetPitch,  Time.deltaTime * 3f);
    }

    AudioClip GenerateClip()
    {
        float[] data = new float[sampleRate]; // 1秒ループ
        for (int i = 0; i < sampleRate; i++)
        {
            float t = (float)i / sampleRate;
            float s = 0f;
            // 差の異なる複数ペアを重ねて複雑な波を作る
            // 低音成分で重さを加える
            s += 0.20f * Mathf.Sin(2f * Mathf.PI * 60f * t);
            // ペア1: 差3Hz → ゆっくりした波
            s += 0.35f * Mathf.Sin(2f * Mathf.PI * 280f * t);
            s += 0.35f * Mathf.Sin(2f * Mathf.PI * 283f * t);
            // ペア2: 差6Hz → 中程度の波
            s += 0.25f * Mathf.Sin(2f * Mathf.PI * 400f * t);
            s += 0.25f * Mathf.Sin(2f * Mathf.PI * 406f * t);
            // ペア3: 差10Hz → 速い波
            s += 0.15f * Mathf.Sin(2f * Mathf.PI * 560f * t);
            s += 0.15f * Mathf.Sin(2f * Mathf.PI * 570f * t);
            // 高調波で質感を加える
            s += 0.08f * Mathf.Sin(2f * Mathf.PI * 800f * t);
            data[i] = s * 0.5f;
        }
        var clip = AudioClip.Create("PropellerBuzz", sampleRate, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
