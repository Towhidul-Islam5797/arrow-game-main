using System.Collections.Generic;
using UnityEngine;
#if MT_DOTWEEN
using DG.Tweening;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Builds and maintains the dot grid used by arrow lines, both in editor and at runtime.
    /// </summary>
    [ExecuteAlways]
    public class GridManager : MonoBehaviour
    {
        public static GridManager I { get; private set; }

        [SerializeField] private bool buildInEditor = false;

        [Header("Grid")]
        [Min(1)] public int width = 10;
        [Min(1)] public int height = 10;
        [Min(0.1f)] public float spacing = 1f;
        public Vector2 origin;

        [Header("Prefabs")]
        public Dot dotPrefab;
        [Header("Animation Settings")]
        [SerializeField] private float scaleAmount = 2f;
        [SerializeField] private float duration = 1f;
        [Tooltip("Yes: Change Material color, No: Change Sprite color")]
        [SerializeField] private bool changeColorOfMaterial = false;
        [SerializeField] private Color lightBlue = new Color(0.5f, 0.8f, 1f, 1f);

        [Header("Runtime")]
        public Transform dotsParent;

        private Dot[,] dots;
        private readonly Dictionary<Vector2Int, Dot> dotByGrid = new();

#if UNITY_EDITOR
        private bool rebuildScheduled = false;
#endif

        void Awake() { I = this; }
        void OnEnable() { I = this; }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!enabled || !gameObject.activeInHierarchy) return;
            if (buildInEditor && !rebuildScheduled)
            {
                rebuildScheduled = true;
                // Defer rebuild to avoid SendMessage errors during OnValidate
                EditorApplication.delayCall += DelayedRebuild;
            }
        }

        private void DelayedRebuild()
        {
            rebuildScheduled = false;
            RebuildSmart();
        }
#endif

        // ---------- SMART REBUILD ----------
        public void RebuildSmart()
        {
            if (!dotPrefab) return;

            // Ensure parent
            if (!dotsParent)
            {
                var t = transform.Find("_Dots");
                dotsParent = t ? t : new GameObject("_Dots").transform;
                dotsParent.SetParent(transform, false);
            }

            // Gather existing children -> map by their stored G
            var existing = new Dictionary<Vector2Int, Dot>();
            var toDestroy = new List<GameObject>();

            foreach (Transform child in dotsParent)
            {
                var d = child.GetComponent<Dot>();
                if (d == null) { toDestroy.Add(child.gameObject); continue; }

                // Keep only those still in-bounds & not duplicated
                if (InBounds(d.G) && !existing.ContainsKey(d.G))
                {
                    existing.Add(d.G, d);
                }
                else
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            // Destroy extras (out-of-bounds or dupes)
            for (int i = 0; i < toDestroy.Count; i++)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(toDestroy[i]);
                else Destroy(toDestroy[i]);
#else
                Destroy(toDestroy[i]);
#endif
            }

            // Build the new grid array, reusing existing dots where possible
            var newDots = new Dot[width, height];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var g = new Vector2Int(x, y);
                    Dot dot;
                    if (existing.TryGetValue(g, out dot))
                    {
                        // Reuse & just reposition/rename
                        dot.transform.position = GridToWorld(g);
                        dot.G = g;
                        dot.name = $"Dot_{x}_{y}";
                    }
                    else
                    {
                        // Create new only if needed
                        dot = Instantiate(dotPrefab, GridToWorld(g), Quaternion.identity, dotsParent);
                        dot.G = g;
                        dot.name = $"Dot_{x}_{y}";
                    }
                    newDots[x, y] = dot;
                }

            dots = newDots;
            dotByGrid.Clear();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var dot = newDots[x, y];
                    if (dot)
                    {
                        dotByGrid[dot.G] = dot;
                        dot.ShowDot();
                    }
                }
        }

        public void DestroyGrid(bool inEditor = false)
        {
            foreach (var dot in dotByGrid.Values)
            {
                if (dot)
                {
                    if (inEditor) DestroyImmediate(dot.gameObject);
                    else Destroy(dot.gameObject);
                }
            }
            dotByGrid.Clear();
            dots = null;
        }

        public void ClearGridOccupant()
        {
            foreach (var dot in dotByGrid.Values)
            {
                if (dot) dot.occupant = null;
            }
        }

        // ---------- HELPERS ----------
        public Dot GetDotByGrid(Vector2Int g) => dotByGrid.TryGetValue(g, out var dot) ? dot : null;
        public bool InBounds(Vector2Int g) =>
            g.x >= 0 && g.y >= 0 && g.x < width && g.y < height;

        public Vector3 GridToWorld(Vector2Int g)
        {
            float x = origin.x + g.x * spacing;
            float y = origin.y + g.y * spacing;
            return new Vector3(x, y, 0);
        }

        public Vector2Int WorldToGrid(Vector3 w)
        {
            int gx = Mathf.RoundToInt((w.x - origin.x) / spacing);
            int gy = Mathf.RoundToInt((w.y - origin.y) / spacing);
            return new Vector2Int(gx, gy);
        }

        public Dot GetDot(Vector2Int g) => InBounds(g) ? dots[g.x, g.y] : null;

        public Dot GetDotFromWorld(Vector3 w) => GetDot(WorldToGrid(w));

        public Vector2Int Step(Vector2Int g, Dir d) => d switch
        {
            Dir.Up => g + Vector2Int.up,
            Dir.Right => g + Vector2Int.right,
            Dir.Down => g + Vector2Int.down,
            _ => g + Vector2Int.left,
        };

        // Put these near your other helpers in GridManager

        // Z plane of your dots (used to project mouse/touch correctly)
        public float dotsPlaneZ => dotsParent ? dotsParent.position.z : 0f;

        // Robust picker: find the nearest Dot within a radius in world space
        public Dot GetNearestDotFromWorld(Vector3 w, float maxDist = 0.45f)
        {
            if (!dotsParent) return null;
            Dot best = null;
            float bestSqr = maxDist * maxDist;

            foreach (Transform child in dotsParent)
            {
                var d = child.GetComponent<Dot>();
                if (!d) continue;
                float sq = (d.transform.position - w).sqrMagnitude;
                if (sq < bestSqr) { bestSqr = sq; best = d; }
            }
            return best;
        }

        public void DeleteNotUsedDots()
        {
            foreach (Transform child in dotsParent)
            {
                var d = child.GetComponent<Dot>();
                if (!d) continue;
                if (d.IsFree) d.HideDot();
            }
        }

        public void ApplyFromAsset(LevelAsset asset)
        {
            if (asset == null) return;
            var g = asset.grid;
            width = Mathf.Max(1, g.width);
            height = Mathf.Max(1, g.height);
            spacing = Mathf.Max(0.01f, g.spacing);
            origin = g.origin;

            RebuildSmart();
        }


        // Add inside GridManager
        public GridDef ToGridDef()
        {
            return new GridDef
            {
                width = width,
                height = height,
                spacing = spacing,
                origin = origin
            };
        }

        // (Optional) for loading from an asset:
        public void ApplyGrid(GridDef def)
        {
            if (def == null) return;
            width = Mathf.Max(1, def.width);
            height = Mathf.Max(1, def.height);
            spacing = Mathf.Max(0.01f, def.spacing);
            origin = def.origin;
            RebuildSmart();
        }

        // ---------- ANIMATIONS ----------
        public void AnimateDotsOnLevelStart()
        {
            if (dots == null) return;

            Color lightBlue = new Color(0.5f, 0.8f, 1f, 1f); // Light blue color
            float duration = 1f;
            float scaleAmount = 1.3f;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var dot = dots[x, y];
                    if (!dot) continue;

                    var spriteRenderer = dot.GetComponent<SpriteRenderer>();
                    if (!spriteRenderer) continue;

                    // Store original values
                    Vector3 originalScale = dot.transform.localScale;
                    Color originalColor = spriteRenderer.color;

                    // Reset to original state first
                    dot.transform.localScale = originalScale;
                    spriteRenderer.color = originalColor;

#if MT_DOTWEEN
                    // Create bounce scale animation
                    dot.transform.DOScale(originalScale * scaleAmount, duration * 0.5f)
                        .SetEase(Ease.OutQuad)
                        .OnComplete(() =>
                        {
                            dot.transform.DOScale(originalScale, duration * 0.5f)
                                .SetEase(Ease.InOutQuad);
                        });

                    // Color fade to light blue and back
                    spriteRenderer.DOColor(lightBlue, duration * 0.5f)
                        .SetEase(Ease.InOutQuad)
                        .OnComplete(() =>
                        {
                            spriteRenderer.DOColor(originalColor, duration * 0.5f)
                                .SetEase(Ease.InOutQuad);
                        });
#else
                    // Fallback: instant state change without tween
                    dot.transform.localScale = originalScale;
                    spriteRenderer.color = originalColor;
#endif
                }
        }

        public void AnimateDotsOnLevelComplete()
        {
            if (dots == null) return;
            Color originalColor = Color.white;

            // Handle material color change once if all dots share the same material
            Material materialToChange = null;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var dot = dots[x, y];
                    if (!dot) continue;

                    // Store original values
                    Vector3 originalScale = dot.transform.localScale;

                    // Reset to original state first
                    dot.transform.localScale = originalScale;

#if MT_DOTWEEN
                    // Create bounce scale animation with elastic ease for more celebration
                    dot.transform.DOScale(originalScale * scaleAmount, duration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                        dot.transform.DOScale(originalScale, duration)
                            .SetEase(Ease.InOutQuad);
                    });

                    // Handle sprite color change per dot (each sprite has its own color)
                    if (!changeColorOfMaterial)
                    {
                        SpriteRenderer _spriteRenderer = dot.GetComponent<SpriteRenderer>();
                        if (_spriteRenderer != null)
                        {
                            originalColor = _spriteRenderer.color;
                            _spriteRenderer.color = originalColor;
                            _spriteRenderer.DOColor(lightBlue, duration)
                                .SetEase(Ease.InOutQuad)
                                .OnComplete(() =>
                                {
                                    _spriteRenderer.DOColor(originalColor, duration)
                                        .SetEase(Ease.InOutQuad);
                                });
                        }
                    }
                    else if (changeColorOfMaterial)
                    {
                        materialToChange = dot.GetComponent<SpriteRenderer>().material;
                        if (materialToChange != null)
                        {
                            originalColor = materialToChange.color;
                            materialToChange.color = originalColor;
                            materialToChange.DOColor(lightBlue, duration / 2)
                                .SetEase(Ease.InOutQuad).SetLoops(4, LoopType.Yoyo)
                                .OnComplete(() =>
                                {
                                    materialToChange.DOColor(originalColor, duration)
                                        .SetEase(Ease.InOutQuad);
                                });
                        }
                    }
#else
                    // Fallback: instant state change without tween
                    dot.transform.localScale = originalScale;
                    if (!changeColorOfMaterial)
                    {
                        SpriteRenderer _spriteRenderer = dot.GetComponent<SpriteRenderer>();
                        if (_spriteRenderer != null)
                        {
                            _spriteRenderer.color = originalColor;
                        }
                    }
                    else if (changeColorOfMaterial)
                    {
                        materialToChange = dot.GetComponent<SpriteRenderer>().material;
                        if (materialToChange != null)
                        {
                            materialToChange.color = originalColor;
                        }
                    }
#endif
                }
        }
    }

}
