using UnityEngine;

/// <summary>
/// Deterministic Built-in Render Pipeline display transform for the visual-fidelity benchmark.
/// It preserves physical scene lighting and performs only global exposure/tonemapping; no bloom,
/// sharpening, chromatic aberration, vignette or local-contrast effect is allowed to hide defects.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class QualityBlockFilmicTonemap : MonoBehaviour
{
    [SerializeField] private Shader filmicShader;
    [SerializeField, Range(-2f, 1f)] private float exposureEV = -0.45f;
    [SerializeField, Range(0.85f, 1.10f)] private float contrast = 1.03f;
    [SerializeField, Range(0.85f, 1.05f)] private float saturation = 0.97f;
    [SerializeField, Range(0f, 0.06f)] private float shadowSoftening = 0.025f;

    private Material material;

    public Shader FilmicShader => filmicShader;
    public float ExposureEV => exposureEV;
    public float Contrast => contrast;
    public float Saturation => saturation;
    public float ShadowSoftening => shadowSoftening;

    public void Configure(Shader shader, float ev, float displayContrast, float displaySaturation, float toeSoftening)
    {
        filmicShader = shader;
        exposureEV = ev;
        contrast = displayContrast;
        saturation = displaySaturation;
        shadowSoftening = toeSoftening;
        RebuildMaterial();
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

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!SystemInfo.supportsImageEffects || filmicShader == null || !filmicShader.isSupported)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (material == null || material.shader != filmicShader)
            RebuildMaterial();

        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetFloat("_ExposureMultiplier", Mathf.Pow(2f, exposureEV));
        material.SetFloat("_Contrast", contrast);
        material.SetFloat("_Saturation", saturation);
        material.SetFloat("_ShadowSoftening", shadowSoftening);
        Graphics.Blit(source, destination, material, 0);
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
