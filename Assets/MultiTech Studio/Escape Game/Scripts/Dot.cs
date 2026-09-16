using UnityEngine;


namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Represents a single grid point that can be occupied by an arrow line.
    /// </summary>
    public class Dot : MonoBehaviour
    {
        public Vector2Int G;

        public ArrowLine occupant;

        public bool IsFree => occupant == null;

        public void HideDot()
        {
            if (TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                spriteRenderer.enabled = false;
            }
        }
        public void ShowDot()
        {
            if (TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                spriteRenderer.enabled = true;
            }
        }
    }
}
