using UnityEngine;

public class CameraHandheld : MonoBehaviour
{
    [Header("Intensidade do movimento")]
    public float posAmplitude = 0.05f;   // Quanto a câmera se move
    public float rotAmplitude = 0.5f;    // Quanto a câmera rotaciona

    [Header("Velocidade do movimento")]
    public float posFrequency = 1.0f;
    public float rotFrequency = 1.5f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        float time = Time.time;

        // Movimento suave usando Perlin Noise
        float xPos = (Mathf.PerlinNoise(time * posFrequency, 0f) - 0.5f) * 2f;
        float yPos = (Mathf.PerlinNoise(0f, time * posFrequency) - 0.5f) * 2f;

        Vector3 offset = new Vector3(xPos, yPos, 0f) * posAmplitude;

        transform.localPosition = initialPosition + offset;

        // Rotação leve (mais realista ainda)
        float xRot = (Mathf.PerlinNoise(time * rotFrequency, 1f) - 0.5f) * 2f;
        float yRot = (Mathf.PerlinNoise(1f, time * rotFrequency) - 0.5f) * 2f;

        Quaternion rotationOffset = Quaternion.Euler(xRot * rotAmplitude, yRot * rotAmplitude, 0f);

        transform.localRotation = initialRotation * rotationOffset;
    }
}