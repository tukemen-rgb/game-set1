using UnityEngine;

/// <summary>
/// Deterministic Built-in Render Pipeline display transform for the visual-fidelity benchmark.
/// It preserves physical scene lighting and performs only global exposure/tonemapping; no bloom,
/// sharpening, chromatic aberration, vignette or local-contrast effect is allowed to hide defects.
///
/// ImageEffectTransformsToLDR is deliberate on OnRenderImage: the source reaching this effect must
/// remain HDR so scene-linear highlights survive until the filmic shoulder, while the destination is
/// explicitly the LDR buffer consumed by the native PNG evidence path. Runtime telemetry is evidence
/// plumbing only; it never awards Visual Fidelity points.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class QualityBlockFilmicTonemap : MonoBehaviour
{
    private const int NativeEvidenceWidth = 3840;
    private const int NativeEvidenceHeight = 2160;

    [SerializeField] private Shader filmicShader;
    [SerializeField, Range(-2f, 1f)] private float exposureEV = -0.45f;
    [SerializeField, Range(0.85f, 1.10f)] private float contrast = 1.03f;
    [SerializeField, Range(0.85f, 1.05f)] private float saturation = 0.97f;
    [SerializeField, Range(0f, 0.06f)] private float shadowSoftening = 0.025f;

    private Material material;

    // Runtime-only capture telemetry. These counters are reset by the authoritative native-4K
    // review packet immediately before a guarded still or temporal sequence begins.
    private int renderInvocationCount;
    private int tonemapAppliedInvocationCount;
    private int native4KInvocationCount;
    private int native4KTonemapAppliedCount;
    private int fallbackInvocationCount;
    private int hdrSourceInvocationCount;
    private int ldrDestinationInvocationCount;
    private int linearHdrSourceInvocationCount;
    private int srgbLdrDestinationInvocationCount;
    private RenderTextureFormat lastSourceFormat = RenderTextureFormat.Default;
    private RenderTextureFormat lastDestinationFormat = RenderTextureFormat.Default;
    private int lastSourceWidth;
    private int lastSourceHeight;
    private int lastDestinationWidth;
    private int lastDestinationHeight;
    private bool lastDestinationWasNull;
    private bool lastSourceSrgb;
    private bool lastDestinationSrgb;

    public Shader FilmicShader => filmicShader;
    public float ExposureEV => exposureEV;
    public float Contrast => contrast;
    public float Saturation => saturation;
    public float ShadowSoftening => shadowSoftening;

    public int RenderInvocationCount => renderInvocationCount;
    public int TonemapAppliedInvocationCount => tonemapAppliedInvocationCount;
    public int Native4KInvocationCount => native4KInvocationCount;
    public int Native4KTonemapAppliedCount => native4KTonemapAppliedCount;
    public int FallbackInvocationCount => fallbackInvocationCount;
    public int HdrSourceInvocationCount => hdrSourceInvocationCount;
    public int LdrDestinationInvocationCount => ldrDestinationInvocationCount;
    public int LinearHdrSourceInvocationCount => linearHdrSourceInvocationCount;
    public int SrgbLdrDestinationInvocationCount => srgbLdrDestinationInvocationCount;
    public RenderTextureFormat LastSourceFormat => lastSourceFormat;
    public RenderTextureFormat LastDestinationFormat => lastDestinationFormat;
    public int LastSourceWidth => lastSourceWidth;
    public int LastSourceHeight => lastSourceHeight;
    public int LastDestinationWidth => lastDestinationWidth;
    public int LastDestinationHeight => lastDestinationHeight;
    public bool LastDestinationWasNull => lastDestinationWasNull;
    public bool LastSourceSrgb => lastSourceSrgb;
    public bool LastDestinationSrgb => lastDestinationSrgb;

    public void Configure(Shader shader, float ev, float displayContrast, float displaySaturation, float toeSoftening)
    {
        filmicShader = shader;
        exposureEV = ev;
        contrast = displayContrast;
        saturation = displaySaturation;
        shadowSoftening = toeSoftening;
        RebuildMaterial();
    }

    /// <summary>
    /// Clears runtime-only evidence counters. This does not alter any visual parameter or scene state.
    /// </summary>
    public void ResetRuntimeTelemetry()
    {
        renderInvocationCount = 0;
        tonemapAppliedInvocationCount = 0;
        native4KInvocationCount = 0;
        native4KTonemapAppliedCount = 0;
        fallbackInvocationCount = 0;
        hdrSourceInvocationCount = 0;
        ldrDestinationInvocationCount = 0;
        linearHdrSourceInvocationCount = 0;
        srgbLdrDestinationInvocationCount = 0;
        lastSourceFormat = RenderTextureFormat.Default;
        lastDestinationFormat = RenderTextureFormat.Default;
        lastSourceWidth = 0;
        lastSourceHeight = 0;
        lastDestinationWidth = 0;
        lastDestinationHeight = 0;
        lastDestinationWasNull = false;
        lastSourceSrgb = false;
        lastDestinationSrgb = false;
    }

    private void OnEnable()
    {
        RebuildMaterial();
    }

    private void OnDisable()
    {
        DestroyMaterial();
    }

    private void OnDestroy()
    {
        DestroyMaterial();
    }

    [ImageEffectTransformsToLDR]
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        renderInvocationCount++;

        bool native4K = source != null && source.width == NativeEvidenceWidth && source.height == NativeEvidenceHeight;
        if (native4K)
            native4KInvocationCount++;

        if (source != null)
        {
            lastSourceFormat = source.format;
            lastSourceWidth = source.width;
            lastSourceHeight = source.height;
            lastSourceSrgb = source.sRGB;
            bool sourceIsHdr = IsHdrFormat(source.format);
            if (sourceIsHdr)
            {
                hdrSourceInvocationCount++;
                if (!source.sRGB)
                    linearHdrSourceInvocationCount++;
            }
        }

        lastDestinationWasNull = destination == null;
        if (destination != null)
        {
            lastDestinationFormat = destination.format;
            lastDestinationWidth = destination.width;
            lastDestinationHeight = destination.height;
            lastDestinationSrgb = destination.sRGB;
            bool destinationIsLdr = !IsHdrFormat(destination.format);
            if (destinationIsLdr)
            {
                ldrDestinationInvocationCount++;
                if (destination.sRGB)
                    srgbLdrDestinationInvocationCount++;
            }
        }
        else
        {
            lastDestinationWidth = 0;
            lastDestinationHeight = 0;
            lastDestinationSrgb = false;
        }

        if (!SystemInfo.supportsImageEffects || filmicShader == null || !filmicShader.isSupported)
        {
            fallbackInvocationCount++;
            Graphics.Blit(source, destination);
            return;
        }

        if (material == null || material.shader != filmicShader)
            RebuildMaterial();

        if (material == null)
        {
            fallbackInvocationCount++;
            Graphics.Blit(source, destination);
            return;
        }

        material.SetFloat("_ExposureMultiplier", Mathf.Pow(2f, exposureEV));
        material.SetFloat("_Contrast", contrast);
        material.SetFloat("_Saturation", saturation);
        material.SetFloat("_ShadowSoftening", shadowSoftening);
        Graphics.Blit(source, destination, material, 0);

        tonemapAppliedInvocationCount++;
        if (native4K)
            native4KTonemapAppliedCount++;
    }

    private static bool IsHdrFormat(RenderTextureFormat format)
    {
        return format == RenderTextureFormat.ARGBHalf ||
               format == RenderTextureFormat.ARGBFloat ||
               format == RenderTextureFormat.RGB111110Float ||
               format == RenderTextureFormat.DefaultHDR;
    }

    private void RebuildMaterial()
    {
        if (filmicShader == null || !filmicShader.isSupported)
        {
            DestroyMaterial();
            return;
        }

        if (material != null && material.shader == filmicShader)
            return;

        DestroyMaterial();
        material = new Material(filmicShader)
        {
            name = "MAT_Runtime_NewTownFilmicTonemap",
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    private void DestroyMaterial()
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);

        material = null;
    }
}
