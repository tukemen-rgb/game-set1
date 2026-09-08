using UnityEngine;

/// <summary>
/// Stable hand-off point between the generated benchmark geometry and future authored art.
/// The editor pipeline keeps the generated fallback intact until a replacement prefab/model exists.
/// </summary>
[DisallowMultipleComponent]
public sealed class QualityBlockArtSlot : MonoBehaviour
{
    [SerializeField] private string slotId;
    [SerializeField] private string expectedAssetName;
    [SerializeField] private GameObject fallbackRoot;
    [SerializeField] private GameObject authoredInstance;

    public string SlotId => slotId;
    public string ExpectedAssetName => expectedAssetName;
    public GameObject FallbackRoot => fallbackRoot;
    public GameObject AuthoredInstance => authoredInstance;
    public bool IsUsingAuthoredArt => authoredInstance != null && authoredInstance.activeSelf;

    public void Configure(string id, string expectedName, GameObject fallback)
    {
        slotId = id;
        expectedAssetName = expectedName;
        fallbackRoot = fallback;
        ApplyVisibility();
    }

    public void SetAuthoredInstance(GameObject instance)
    {
        authoredInstance = instance;
        ApplyVisibility();
    }

    public void ApplyVisibility()
    {
        bool hasAuthored = authoredInstance != null;
        if (fallbackRoot != null)
            fallbackRoot.SetActive(!hasAuthored);
        if (authoredInstance != null)
            authoredInstance.SetActive(true);
    }
}
