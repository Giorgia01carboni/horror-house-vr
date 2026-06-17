using UnityEngine;

/// Attach alongside InspectableObject on any prop that should glow on hover.
/// ObjectInspector calls SetHighlight(true/false) when the player looks at or away from it.
[RequireComponent(typeof(InspectableObject))]
public class ObjectHighlighter : MonoBehaviour
{
    [SerializeField] Color emissionColor = new Color(0.18f, 0.14f, 0.06f); // warm candlelight glow

    Renderer[] rends;
    Color[]    origEmissions;
    bool[]     origEmissionEnabled;
    bool       highlighted;
    bool       instancesCreated;

    void Awake()
    {
        rends               = GetComponentsInChildren<Renderer>(true);
        origEmissions       = new Color[rends.Length];
        origEmissionEnabled = new bool[rends.Length];

        // Read originals from shared materials before we ever create instances
        for (int i = 0; i < rends.Length; i++)
        {
            var mat = rends[i].sharedMaterial;
            if (mat == null) continue;
            origEmissions[i]       = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            origEmissionEnabled[i] = mat.IsKeywordEnabled("_EMISSION");
        }
    }

    public void SetHighlight(bool on)
    {
        if (on == highlighted) return;
        highlighted = on;

        if (!on && !instancesCreated) return;
        instancesCreated = true;

        for (int i = 0; i < rends.Length; i++)
        {
            var mat = rends[i].material; // creates/reuses per-instance material
            if (!mat.HasProperty("_EmissionColor")) continue;

            if (on)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
            }
            else
            {
                if (!origEmissionEnabled[i]) mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", origEmissions[i]);
            }
        }
    }
}
