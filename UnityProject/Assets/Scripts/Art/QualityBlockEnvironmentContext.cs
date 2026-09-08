using System;
using UnityEngine;

[Flags]
public enum NewTownSurfaceExposure
{
    None = 0,
    SunExposed = 1 << 0,
    RainExposed = 1 << 1,
    Sheltered = 1 << 2,
    GroundContact = 1 << 3,
    Recessed = 1 << 4,
    NorthFacing = 1 << 5,
    UpwardFacing = 1 << 6,
}

[Flags]
public enum NewTownStainSource
{
    None = 0,
    RainLedge = 1 << 0,
    GroundSplash = 1 << 1,
    DrainRunoff = 1 << 2,
    FerrousFixture = 1 << 3,
    HumanContact = 1 << 4,
    FootTraffic = 1 << 5,
    UVExposure = 1 << 6,
    RecessGrime = 1 << 7,
}

/// <summary>
/// Scene-wide physical context for the benchmark block. Solar time is deliberately stored as
/// local apparent solar time so the stage remains deterministic and does not depend on OS clock,
/// timezone databases, or a particular real-world city.
/// </summary>
public sealed class QualityBlockEnvironmentContext : MonoBehaviour
{
    [SerializeField] float latitudeDegrees = 35.6f;
    [SerializeField] int dayOfYear = 213;
    [SerializeField] float solarTimeHours = 14f;
    [SerializeField] bool recentRain;
    [SerializeField] float recentRainAmount;

    public float LatitudeDegrees => latitudeDegrees;
    public int DayOfYear => dayOfYear;
    public float SolarTimeHours => solarTimeHours;
    public bool RecentRain => recentRain;
    public float RecentRainAmount => recentRainAmount;

    public void Configure(float latitude, int day, float solarTime, bool hasRecentRain, float rainAmount)
    {
        latitudeDegrees = Mathf.Clamp(latitude, -66f, 66f);
        dayOfYear = Mathf.Clamp(day, 1, 365);
        solarTimeHours = Mathf.Repeat(solarTime, 24f);
        recentRain = hasRecentRain;
        recentRainAmount = Mathf.Clamp01(rainAmount);
    }

    public SolarSample CalculateSolarSample()
    {
        return NewTownSolarModel.Calculate(latitudeDegrees, dayOfYear, solarTimeHours);
    }

    public void ApplyToDirectionalLight(Light light)
    {
        if (light == null) return;

        SolarSample sample = CalculateSolarSample();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.LookRotation(sample.RayDirection, Vector3.up);
        light.intensity = Mathf.Lerp(0.82f, 1.18f, Mathf.Clamp01(sample.ElevationDegrees / 70f));
        light.color = Color.white;
        light.useColorTemperature = true;
        light.colorTemperature = Mathf.Lerp(5100f, 5850f, Mathf.Clamp01(sample.ElevationDegrees / 65f));
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.92f;
        light.shadowBias = 0.04f;
        light.shadowNormalBias = 0.28f;
    }
}

[Serializable]
public struct SolarSample
{
    public float ElevationDegrees;
    public float AzimuthDegrees;
    public Vector3 DirectionToSun;
    public Vector3 RayDirection;
    public float HorizontalShadowPerMetre;
}

public static class NewTownSolarModel
{
    /// <summary>
    /// +X is east, +Z is north, +Y is up. Azimuth is clockwise from north.
    /// </summary>
    public static SolarSample Calculate(float latitudeDegrees, int dayOfYear, float solarTimeHours)
    {
        float lat = latitudeDegrees * Mathf.Deg2Rad;
        float declinationDegrees = 23.44f * Mathf.Sin((360f / 365f) * (284f + dayOfYear) * Mathf.Deg2Rad);
        float dec = declinationDegrees * Mathf.Deg2Rad;
        float hourAngle = (15f * (solarTimeHours - 12f)) * Mathf.Deg2Rad;

        float east = -Mathf.Cos(dec) * Mathf.Sin(hourAngle);
        float north = Mathf.Sin(dec) * Mathf.Cos(lat) - Mathf.Cos(dec) * Mathf.Sin(lat) * Mathf.Cos(hourAngle);
        float up = Mathf.Sin(lat) * Mathf.Sin(dec) + Mathf.Cos(lat) * Mathf.Cos(dec) * Mathf.Cos(hourAngle);

        Vector3 toSun = new Vector3(east, up, north).normalized;
        float elevation = Mathf.Asin(Mathf.Clamp(toSun.y, -1f, 1f)) * Mathf.Rad2Deg;
        float azimuth = Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg;
        if (azimuth < 0f) azimuth += 360f;

        float elevationRad = Mathf.Max(1f, elevation) * Mathf.Deg2Rad;
        float shadowPerMetre = 1f / Mathf.Tan(elevationRad);

        return new SolarSample
        {
            ElevationDegrees = elevation,
            AzimuthDegrees = azimuth,
            DirectionToSun = toSun,
            RayDirection = -toSun,
            HorizontalShadowPerMetre = shadowPerMetre,
        };
    }
}

/// <summary>
/// Cause-based metadata for a physical surface/assembly. These values are intentionally explicit:
/// visual weathering tools should derive stains and wear from them instead of adding free-form noise.
/// </summary>
public sealed class QualityBlockWeatheringSurface : MonoBehaviour
{
    [SerializeField] NewTownSurfaceExposure exposure;
    [SerializeField] NewTownStainSource stainSources;
    [SerializeField, Range(0f, 1f)] float rainExposure;
    [SerializeField, Range(0f, 1f)] float sunExposure;
    [SerializeField, Range(0f, 1f)] float splashExposure;
    [SerializeField, Range(0f, 1f)] float humanContact;

    public NewTownSurfaceExposure Exposure => exposure;
    public NewTownStainSource StainSources => stainSources;
    public float RainExposure => rainExposure;
    public float SunExposure => sunExposure;
    public float SplashExposure => splashExposure;
    public float HumanContact => humanContact;

    public void Configure(NewTownSurfaceExposure surfaceExposure, NewTownStainSource sources,
        float rain, float sun, float splash, float contact)
    {
        exposure = surfaceExposure;
        stainSources = sources;
        rainExposure = Mathf.Clamp01(rain);
        sunExposure = Mathf.Clamp01(sun);
        splashExposure = Mathf.Clamp01(splash);
        humanContact = Mathf.Clamp01(contact);
    }

    public float CalculateMoistureRetention()
    {
        float sheltered = (exposure & NewTownSurfaceExposure.Sheltered) != 0 ? 0.22f : 0f;
        float north = (exposure & NewTownSurfaceExposure.NorthFacing) != 0 ? 0.18f : 0f;
        float recess = (exposure & NewTownSurfaceExposure.Recessed) != 0 ? 0.16f : 0f;
        return Mathf.Clamp01(rainExposure * 0.58f + sheltered + north + recess - sunExposure * 0.35f);
    }

    public float CalculateUVFade()
    {
        if ((stainSources & NewTownStainSource.UVExposure) == 0) return 0f;
        return Mathf.Clamp01(sunExposure * 0.72f);
    }

    public float CalculateGroundSoiling()
    {
        if ((stainSources & NewTownStainSource.GroundSplash) == 0) return 0f;
        return Mathf.Clamp01(splashExposure * 0.85f);
    }
}
