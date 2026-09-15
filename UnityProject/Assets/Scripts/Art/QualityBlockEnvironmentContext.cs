using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

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

        // Solar direction and shadow-map quality are one capture invariant. Historically the weathering
        // pass reapplied this solar light after the shadow-stability pass and silently restored different
        // per-light bias values. Reapplying the complete contract here prevents a fresh scene rebuild from
        // invalidating the shadow settings that were validated before it.
        QualityBlockSolarShadowRuntimeContract.Apply(this, light);

        QualityBlockSolarShadowPreRenderGuard guard = GetComponent<QualityBlockSolarShadowPreRenderGuard>();
        if (guard == null) guard = gameObject.AddComponent<QualityBlockSolarShadowPreRenderGuard>();
        guard.Configure(this, light);
    }
}

/// <summary>
/// Runtime-safe source of truth for the deterministic midsummer sun and shadow-map state. The editor
/// QA layer validates the same values, while the pre-render guard checks them immediately before the
/// benchmark MainCamera culls. This class assigns no Visual Fidelity points.
/// </summary>
public static class QualityBlockSolarShadowRuntimeContract
{
    public const float BenchmarkLatitudeDegrees = 35.6f;
    public const int BenchmarkDayOfYear = 213;
    public const float BenchmarkSolarTimeHours = 14f;

    public const float ShadowDistance = 72f;
    public const float ShadowBias = 0.025f;
    public const float ShadowNormalBias = 0.38f;
    public const float ShadowNearPlane = 0.10f;
    public const float GlobalShadowNearPlaneOffset = 2.0f;
    public const float ShadowStrength = 0.92f;
    public const float LodBias = 2.0f;
    public const int RequestedMsaa = 8;

    public static readonly Vector3 CascadeSplit = new Vector3(0.10f, 0.25f, 0.50f);

    public static void Apply(QualityBlockEnvironmentContext context, Light sun)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (sun == null) throw new ArgumentNullException(nameof(sun));

        SolarSample sample = context.CalculateSolarSample();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.LookRotation(sample.RayDirection, Vector3.up);
        sun.intensity = Mathf.Lerp(0.82f, 1.18f, Mathf.Clamp01(sample.ElevationDegrees / 70f));
        sun.color = Color.white;
        sun.useColorTemperature = true;
        sun.colorTemperature = Mathf.Lerp(5100f, 5850f, Mathf.Clamp01(sample.ElevationDegrees / 65f));
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = ShadowStrength;
        sun.shadowResolution = LightShadowResolution.VeryHigh;
        sun.shadowBias = ShadowBias;
        sun.shadowNormalBias = ShadowNormalBias;
        sun.shadowNearPlane = ShadowNearPlane;
        RenderSettings.sun = sun;

        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadowCascade4Split = CascadeSplit;
        QualitySettings.shadowDistance = ShadowDistance;
        QualitySettings.shadowNearPlaneOffset = GlobalShadowNearPlaneOffset;
        QualitySettings.antiAliasing = RequestedMsaa;
        QualitySettings.lodBias = LodBias;
        QualitySettings.maximumLODLevel = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.realtimeReflectionProbes = true;

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.renderingPath = RenderingPath.Forward;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.allowDynamicResolution = false;
        }

        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(x => x.gameObject.scene.IsValid())
            .ToArray();
        foreach (Renderer renderer in renderers)
        {
            bool transparentOptics = renderer.gameObject.name.StartsWith("FO_Glass_", StringComparison.Ordinal) ||
                                     renderer.gameObject.name.StartsWith("FO_StairGlass_", StringComparison.Ordinal);
            if (transparentOptics)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            else if (renderer.enabled)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        LODGroup[] lodGroups = Resources.FindObjectsOfTypeAll<LODGroup>()
            .Where(x => x.gameObject.scene.IsValid() && x.lodCount >= 2)
            .ToArray();
        foreach (LODGroup lodGroup in lodGroups)
        {
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
        }
    }

    public static void ValidateOrThrow(QualityBlockEnvironmentContext context, Light sun, bool requireMainCamera)
    {
        if (context == null) throw new InvalidOperationException("Solar/shadow capture contract requires one environment context.");
        if (sun == null) throw new InvalidOperationException("Solar/shadow capture contract requires SummerSun.");

        if (Mathf.Abs(context.LatitudeDegrees - BenchmarkLatitudeDegrees) > 0.001f ||
            context.DayOfYear != BenchmarkDayOfYear ||
            Mathf.Abs(context.SolarTimeHours - BenchmarkSolarTimeHours) > 0.001f ||
            context.RecentRain || context.RecentRainAmount > 0.001f)
            throw new InvalidOperationException(
                "Benchmark solar/weather context drifted from the dry midsummer reference sample (35.6 deg N, day 213, 14:00 apparent solar time).");

        Light[] activeDirectional = Resources.FindObjectsOfTypeAll<Light>()
            .Where(x => x.gameObject.scene.IsValid() && x.enabled && x.gameObject.activeInHierarchy && x.type == LightType.Directional)
            .ToArray();
        if (activeDirectional.Length != 1 || activeDirectional[0] != sun)
        {
            string names = string.Join(", ", activeDirectional.Select(x => x.name));
            throw new InvalidOperationException(
                $"Critical solar-coherence failure: expected exactly one active directional light (SummerSun), found {activeDirectional.Length}: {names}");
        }
        if (sun.name != "SummerSun" || RenderSettings.sun != sun)
            throw new InvalidOperationException("Procedural sky/global sun reference must point to the sole SummerSun directional light.");

        SolarSample sample = context.CalculateSolarSample();
        if (sample.ElevationDegrees < 50f || sample.ElevationDegrees > 65f ||
            sample.AzimuthDegrees < 235f || sample.AzimuthDegrees > 250f ||
            sample.HorizontalShadowPerMetre < 0.50f || sample.HorizontalShadowPerMetre > 0.75f)
            throw new InvalidOperationException(
                $"Solar sample left the locked midsummer-afternoon envelope: elevation={sample.ElevationDegrees:F4}, azimuth={sample.AzimuthDegrees:F4}, shadow/m={sample.HorizontalShadowPerMetre:F4}.");

        if (Vector3.Dot(sun.transform.forward.normalized, sample.RayDirection.normalized) < 0.9999f)
            throw new InvalidOperationException("SummerSun transform direction no longer matches the deterministic solar ray.");

        float expectedIntensity = Mathf.Lerp(0.82f, 1.18f, Mathf.Clamp01(sample.ElevationDegrees / 70f));
        float expectedTemperature = Mathf.Lerp(5100f, 5850f, Mathf.Clamp01(sample.ElevationDegrees / 65f));
        if (Mathf.Abs(sun.intensity - expectedIntensity) > 0.002f ||
            !sun.useColorTemperature || Mathf.Abs(sun.colorTemperature - expectedTemperature) > 2f)
            throw new InvalidOperationException("SummerSun intensity/color-temperature drifted from the solar sample.");
        if (sun.shadows != LightShadows.Soft ||
            sun.shadowResolution != LightShadowResolution.VeryHigh ||
            Mathf.Abs(sun.shadowStrength - ShadowStrength) > 0.001f ||
            Mathf.Abs(sun.shadowBias - ShadowBias) > 0.001f ||
            Mathf.Abs(sun.shadowNormalBias - ShadowNormalBias) > 0.001f ||
            Mathf.Abs(sun.shadowNearPlane - ShadowNearPlane) > 0.001f)
            throw new InvalidOperationException("SummerSun shadow state drifted after the last scene rebuild.");

        if (QualitySettings.shadows != ShadowQuality.All ||
            QualitySettings.shadowResolution != ShadowResolution.VeryHigh ||
            QualitySettings.shadowProjection != ShadowProjection.StableFit ||
            QualitySettings.shadowCascades != 4 ||
            (QualitySettings.shadowCascade4Split - CascadeSplit).sqrMagnitude > 0.000001f ||
            Mathf.Abs(QualitySettings.shadowDistance - ShadowDistance) > 0.01f ||
            Mathf.Abs(QualitySettings.shadowNearPlaneOffset - GlobalShadowNearPlaneOffset) > 0.01f ||
            QualitySettings.antiAliasing < 4 ||
            QualitySettings.lodBias < 1.9f || QualitySettings.maximumLODLevel != 0)
            throw new InvalidOperationException("Global shadow/edge quality drifted from the native-4K capture contract.");

        if (requireMainCamera)
        {
            Camera camera = Camera.main;
            if (camera == null || camera.renderingPath != RenderingPath.Forward ||
                !camera.allowHDR || !camera.allowMSAA || camera.allowDynamicResolution)
                throw new InvalidOperationException("MainCamera is not in deterministic Forward HDR+MSAA capture state.");
        }
    }
}

/// <summary>
/// Fail-closed render-time guard for the benchmark MainCamera. Camera.Render invokes pre-cull callbacks
/// in the Built-in Render Pipeline, so a contradictory directional light or reset shadow setting is
/// rejected before a 4K still/temporal frame can be treated as authoritative evidence.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class QualityBlockSolarShadowPreRenderGuard : MonoBehaviour
{
    [SerializeField] QualityBlockEnvironmentContext context;
    [SerializeField] Light sun;

    public QualityBlockEnvironmentContext Context => context;
    public Light Sun => sun;

    public void Configure(QualityBlockEnvironmentContext environmentContext, Light directionalSun)
    {
        context = environmentContext;
        sun = directionalSun;
        enabled = true;
    }

    void OnEnable()
    {
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
    }

    void OnDisable()
    {
        Camera.onPreCull -= OnCameraPreCull;
    }

    void OnDestroy()
    {
        Camera.onPreCull -= OnCameraPreCull;
    }

    void OnCameraPreCull(Camera camera)
    {
        if (camera == null || context == null || sun == null) return;
        if (!context.gameObject.scene.IsValid() || camera.gameObject.scene != context.gameObject.scene) return;
        if (Camera.main != camera) return;
        QualityBlockSolarShadowRuntimeContract.ValidateOrThrow(context, sun, true);
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
