using UnityEngine;

public class HorrorFog : MonoBehaviour
{
    public static HorrorFog Instance { get; private set; }

    [SerializeField] private Color fogColor = new Color(0.04f, 0.04f, 0.07f, 1f);
    [SerializeField] private float fogDensity = 0.04f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    public void SetInsideHouse(bool inside)
    {
        RenderSettings.fog = !inside;
    }
}
