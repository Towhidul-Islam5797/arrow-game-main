using UnityEngine;
using Random = UnityEngine.Random;


namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Applies a random colour to the line and its associated sprites at runtime.
    /// </summary>
    public class LineColors : MonoBehaviour
    {
        [SerializeField] private Material mainMaterial;
        [SerializeField] private ArrowLine arrowLine;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private SpriteRenderer[] spriteRenderers;

        [SerializeField] private Color[] colors;

        void Start()
        {
            // if (colors == null || colors.Length == 0) return;
            if (mainMaterial == null || lineRenderer == null) return;
            colors = new Color[ThemeManager.Instance.GetActiveTheme().colors.Count];
            for (int i = 0; i < ThemeManager.Instance.GetActiveTheme().colors.Count; i++)
            {
                colors[i] = ThemeManager.Instance.GetActiveTheme().colors[i];
            }

            Color _clr = colors[Random.Range(0, colors.Length)];
            Material _newMaterial = new Material(mainMaterial);
            _newMaterial.color = _clr;

            lineRenderer.material = _newMaterial;
            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer != null) spriteRenderer.color = _clr;
            }
            if (arrowLine != null) arrowLine.SetBaseColor(_newMaterial.color);
        }
    }
}
