using UnityEngine;

// Lightweight warm-light flicker for the placeable environmental fire prefab.
// The flames and smoke themselves come from the Free Fire VFX pack.
[DisallowMultipleComponent]
public class PlaceableFireVFX : MonoBehaviour
{
    [SerializeField] Light fireLight;
    [SerializeField, Min(0f)] float baseIntensity = 2.6f;
    [SerializeField, Range(0f, 1f)] float flickerAmount = 0.28f;
    [SerializeField, Min(0.1f)] float flickerSpeed = 7f;

    float noiseOffset;

    void Awake()
    {
        if (fireLight == null)
            fireLight = GetComponentInChildren<Light>(true);
        noiseOffset = Random.Range(0f, 1000f);
    }

    void Update()
    {
        if (fireLight == null)
            return;

        float noise = Mathf.PerlinNoise(noiseOffset, Time.time * flickerSpeed) * 2f - 1f;
        fireLight.intensity = baseIntensity * (1f + noise * flickerAmount);
    }
}
