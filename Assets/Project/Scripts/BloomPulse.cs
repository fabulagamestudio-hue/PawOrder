using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BloomPulse : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Volume volume;
    [SerializeField] private AudioSource audioSource;

    [Header("Bloom")]
    [SerializeField] private float minIntensity = 0.2f;
    [SerializeField] private float maxIntensity = 2.5f;

    [Header("Audio Reaction")]
    [SerializeField] private float sensitivity = 25f;
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Old Lamp Instability")]
    [SerializeField] private float randomFlickerAmount = 0.4f;
    [SerializeField] private float weaknessChance = 0.15f;
    [SerializeField] private float weakMultiplier = 0.25f;

    private Bloom bloom;
    private float[] samples = new float[256];

    private void Awake()
    {
        if (volume == null)
            volume = GetComponent<Volume>();

        if (volume != null)
            volume.profile.TryGet(out bloom);
    }

    private void Update()
    {
        if (bloom == null || audioSource == null) return;

        audioSource.GetOutputData(samples, 0);

        float rms = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            rms += samples[i] * samples[i];
        }

        rms = Mathf.Sqrt(rms / samples.Length);

        float audioPower = Mathf.Clamp01(rms * sensitivity);

        float targetIntensity = Mathf.Lerp(minIntensity, maxIntensity, audioPower);

        targetIntensity += Random.Range(-randomFlickerAmount, randomFlickerAmount);

        if (Random.value < weaknessChance * Time.deltaTime)
        {
            targetIntensity *= weakMultiplier;
        }

        targetIntensity = Mathf.Clamp(targetIntensity, minIntensity, maxIntensity);

        bloom.intensity.value = Mathf.Lerp(
            bloom.intensity.value,
            targetIntensity,
            Time.deltaTime * smoothSpeed
        );
    }
}