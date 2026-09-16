using System.Collections.Generic;
using UnityEngine;


namespace MultiTechStudio.EscapeGame
{
    public class ArrowFacingLines : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Color lineColor = new Color(0.05f, 0.45f, 0.85f, 0.5f); // blue-ish
        private float lineWidth = 0.2f;
        private int range = 1;                 // how many grid-lines forward to show
        private float cellSize = 1f;           // grid cell spacing in world units
        private float lineLength = 160f;        // how far lines extend (half-length in each direction)
        [SerializeField] private Transform linesParent;         // optional parent for created lines

        // If true, stop drawing stripes when FindDotByGrid returns null for that probe coordinate.
        private bool stopOnMissingDot = true;

        // Prefabless: we'll create LineRenderers at runtime
        List<LineRenderer> activeLines = new List<LineRenderer>();

        public System.Func<Vector2Int, bool> DotExistsAtGrid = null;
        // If you have a FindDotByGrid method, you can set:
        // facingLines.DotExistsAtGrid = (g) => FindDotByGrid(g) != null;

        public void ShowFacingLines(Vector3 headWorldPos, Vector2Int rawFacingDir)
        {
            ClearFacingLines();
            if (rawFacingDir == Vector2Int.zero) return;

            // Snap to dominant cardinal axis (in case of diagonal)
            Vector2Int dir = SnapToCardinal(rawFacingDir);
            if (dir == Vector2Int.zero) return;

            bool facingAlongX = Mathf.Abs(dir.x) == 1; // true => moving along X (left/right)
            float y = headWorldPos.z; // store constant height coord

            for (int step = 1; step <= range; step++)
            {
                // compute probe grid coord (assuming head is at grid center: step * dir)
                Vector2Int probeGrid = new Vector2Int(Mathf.RoundToInt(headWorldPos.x), Mathf.RoundToInt(headWorldPos.y)
                );
                Vector3 center = headWorldPos;

                if (stopOnMissingDot && DotExistsAtGrid != null)
                {
                    if (!DotExistsAtGrid(probeGrid))
                    {
                        // stop drawing further stripes in this direction
                        break;
                    }
                }

                // Build stripe endpoints: if facingAlongX -> vertical stripe (vary Z), else horizontal stripe (vary X)
                Vector3 start, end;
                float halfLen = lineLength * 0.5f;

                if (!facingAlongX)
                {
                    start = new Vector3(center.x, center.y - halfLen, center.z);
                    end = new Vector3(center.x, center.y + halfLen, center.z);
                }
                else
                {
                    start = new Vector3(center.x - halfLen, center.y, center.z);
                    end = new Vector3(center.x + halfLen, center.y, center.z);
                }

                var lr = CreateLineRenderer("FacingLine_" + step);
                lr.positionCount = 2;
                lr.SetPosition(0, start);
                lr.SetPosition(1, end);
                activeLines.Add(lr);
            }
        }

        Vector2Int SnapToCardinal(Vector2Int v)
        {
            if (v == Vector2Int.zero) return Vector2Int.zero;
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
                return new Vector2Int(Mathf.Sign(v.x) == 0 ? 0 : (v.x > 0 ? 1 : -1), 0);
            else
                return new Vector2Int(0, Mathf.Sign(v.y) == 0 ? 0 : (v.y > 0 ? 1 : -1));
        }
        /// <summary> Remove lines </summary>
        public void ClearFacingLines()
        {
            foreach (var lr in activeLines)
            {
                if (lr != null) Destroy(lr.gameObject);
            }
            activeLines.Clear();
        }

        // Create a minimal runtime LineRenderer with a small material.
        LineRenderer CreateLineRenderer(string name)
        {
            GameObject go = new GameObject(name);
            if (linesParent != null) go.transform.SetParent(linesParent, true);
            else go.transform.SetParent(this.transform, true);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 4;
            lr.material = new Material(Shader.Find("Sprites/Default")); // simple shader that supports color
            lr.startColor = lr.endColor = lineColor;
            lr.startWidth = lr.endWidth = lineWidth;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = 54;
            // Optional: smoothness / rounded caps
            lr.alignment = LineAlignment.View;
            return lr;
        }

        // Auto-clear when object disabled/destroyed
        void OnDisable() => ClearFacingLines();
        void OnDestroy() => ClearFacingLines();
    }
}
