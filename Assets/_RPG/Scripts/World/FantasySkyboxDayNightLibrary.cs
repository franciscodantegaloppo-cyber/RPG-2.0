using UnityEngine;

// Kept in a file with the same name as the MonoBehaviour so Unity can serialize the scene
// reference reliably instead of treating it as a runtime-only secondary script.
public sealed class FantasySkyboxDayNightLibrary : MonoBehaviour
{
    public Material sunrise;
    public Material day;
    public Material sunset;
    public Material night;
    public Material moonlessNight;

    public bool HasDaySky => day != null;

    public Material GetForHour(float hour)
    {
        if (hour >= 5f && hour < 8f) return sunrise != null ? sunrise : day;
        if (hour >= 8f && hour < 17f) return day;
        if (hour >= 17f && hour < 20f) return sunset != null ? sunset : day;
        if (hour >= 1f && hour < 5f && moonlessNight != null) return moonlessNight;
        return night != null ? night : day;
    }
}
