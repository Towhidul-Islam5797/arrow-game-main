using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// PLAY MODE painter for Game view:
/// - Press/click on a Dot, drag across neighboring Dots (turns allowed), release to spawn an ArrowLine.
/// - Live preview via LineRenderer (optional).
/// - Mouse + single touch supported.
/// - Editor-only: buttons to save the painted arrows or the grid as prefabs under Assets/Prefabs.
/// </summary>
namespace MultiTechStudio.EscapeGame
{
    public class ArrowLineRuntimePainterL : MonoBehaviour
    {
        [Header("Refs")]
        public GridManager grid;                 // assign your GridManager (auto-fills if left empty)
        public GameObject arrowLinePrefab;       // prefab with LineRenderer + EdgeCollider2D + ArrowLine
        public LineRenderer previewLine;         // optional, for live preview
        public Camera gameCamera;                // assign your main camera here if Camera.main is null

        [Header("Picking")]
        public float pickRadius = 0.45f;         // world units for nearest-dot snapping

        [Header("Preview Style")]
        public int previewSortingOrder = 1000;
        public float previewWidth = 0.12f;

        // ---- runtime state ----
        Camera cam;
        bool dragging;
        readonly List<Dot> path = new();         // full path including start & all turns
        Dot lastDot;
        readonly List<ArrowLine> createdArrows = new();  // track arrows for undo

#if UNITY_EDITOR
        [Header("Screenshot Import (Experimental)")]
        [Tooltip("Enable screenshot-based level import feature")]
        public bool enableScreenshotImport = false;  // Default OFF for safety

        [Tooltip("Treat black pixels as path instead of white (useful for black-line-on-white images)")]
        public bool invertColors = false;

        [Tooltip("Use manual grid dimensions instead of auto-detection")]
        public bool useManualGridSize = false;

        [Tooltip("Manual grid width (only used if useManualGridSize is true)")]
        [Min(1)] public int manualGridWidth = 15;

        [Tooltip("Manual grid height (only used if useManualGridSize is true)")]
        [Min(1)] public int manualGridHeight = 17;

        string status;
        int detectedArrowCount = 0;  // Track detected arrows from screenshot
        readonly List<ArrowLine> screenshotArrows = new();  // Track arrows created from screenshot
#endif
        void Awake()
        {
            cam = gameCamera != null ? gameCamera : Camera.main;
            if (!grid) grid = GridManager.I;

            if (previewLine)
            {
                previewLine.positionCount = 0;
                previewLine.sortingOrder = previewSortingOrder;
                previewLine.startWidth = previewWidth;
                previewLine.endWidth = previewWidth;
                previewLine.useWorldSpace = true;
            }
        }

        void Update()
        {
            if (!GameMode.IsDraw)
            { // disable painting in Play
                if (previewLine) previewLine.positionCount = 0;
                dragging = false;
                return;
            }
            // live status
#if UNITY_EDITOR
            status = $"cam={(cam ? cam.name : "NULL")} grid={(grid ? grid.name : "NULL")} dotsZ={(grid ? grid.dotsPlaneZ : 0):0.00} prefabOK={(arrowLinePrefab ? arrowLinePrefab.GetComponent<ArrowLine>() != null : false)} pathN={path.Count} dragging={dragging}";
#endif

            if (!cam || !grid || !arrowLinePrefab) return;

            if (PointerDownThisFrame())
            {
                var w = PointerWorld();
                var start = grid.GetNearestDotFromWorld(w, pickRadius);
#if UNITY_EDITOR
                status += $" | down @ {w} start={(start ? start.name : "NULL")}";
#endif
                if (start != null)
                {
                    dragging = true;
                    path.Clear();
                    path.Add(start);
                    lastDot = start;
                    UpdatePreview();
                }
            }

            if (dragging && PointerHeld())
            {
                var w = PointerWorld();
                var near = grid.GetNearestDotFromWorld(w, pickRadius);

                if (near && near != lastDot && IsCardinalNeighbour(lastDot.G, near.G))
                {
                    // A) allow immediate backtrack (undo last step)
                    if (path.Count >= 2 && near == path[path.Count - 2])
                    {
                        path.RemoveAt(path.Count - 1);
                        lastDot = path[path.Count - 1];
                        UpdatePreview();
                        return;
                    }

                    // B) BLOCK if 'near' is already somewhere in the path (prevent self-overlap/loops)
                    // (We’ve already handled the one-step backtrack above.)
                    if (path.Contains(near)) return;

                    // C) BLOCK if 'near' is occupied by another arrow
                    if (near.occupant != null) return;

                    // D) Safe to extend
                    path.Add(near);
                    lastDot = near;
                    UpdatePreview();
                }
            }


            if (dragging && PointerUpThisFrame())
            {
#if UNITY_EDITOR
                status += " | up";
#endif
                CommitArrowLine();
                dragging = false;
                path.Clear();
                UpdatePreview();
            }
        }

        // ---------------- Commit & Preview ----------------
        /// <summary>
        /// Instantiates a new arrow line from the current path after validating occupancy and loops.
        /// </summary>
        void CommitArrowLine()
        {
            if (!arrowLinePrefab || path.Count < 2) return;

            var clean = new List<Dot>(path.Count);
            var seen = new HashSet<Dot>();

            for (int i = 0; i < path.Count; i++)
            {
                var d = path[i];

                // Stop before first occupied-by-other
                if (d.occupant != null) break;

                // Stop before first repeat (prevents loops/self-overlap)
                if (seen.Contains(d)) break;

                clean.Add(d);
                seen.Add(d);
            }

            if (clean.Count < 2) return;

            var go = Instantiate(arrowLinePrefab);
            // keep the object tidy at z = -1 (ArrowLine also forces points to -1)
            var p = go.transform.position; p.z = -1f; go.transform.position = p;

            var al = go.GetComponent<ArrowLine>();
            al.nodes = clean;
            al.zOffset = -1f;
            al.occupyAllNodes = true;
            al.SyncVisualImmediate();

            // claim head (ArrowLine.Start will claim the rest if needed)
            var head = al.CurrentHead;
            if (head && head.occupant == null) head.occupant = al;

            // track for undo
            createdArrows.Add(al);
        }



        /// <summary>
        /// Updates the in-scene preview line so designers can see the current stroke while painting.
        /// </summary>
        void UpdatePreview()
        {
            if (!previewLine) return;

            if (path.Count < 1)
            {
                previewLine.positionCount = 0;
                return;
            }

            previewLine.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++)
                previewLine.SetPosition(i, path[i].transform.position);
        }

        /// <summary>
        /// Removes the most recently placed arrow line and frees any occupied dots.
        /// </summary>
        void UndoLastArrow()
        {
            if (createdArrows.Count == 0)
            {
                Debug.Log("No arrows to undo.");
                return;
            }

            // Get the last arrow
            int lastIndex = createdArrows.Count - 1;
            var arrow = createdArrows[lastIndex];

            if (arrow != null)
            {
                // Release all nodes occupied by this arrow
                if (arrow.nodes != null)
                {
                    foreach (var dot in arrow.nodes)
                    {
                        if (dot && dot.occupant == arrow)
                        {
                            dot.occupant = null;
                        }
                    }
                }

                // Destroy the arrow GameObject
                Destroy(arrow.gameObject);
            }

            // Remove from tracking list
            createdArrows.RemoveAt(lastIndex);
            Debug.Log($"Undone last arrow. {createdArrows.Count} arrows remaining.");
        }

        // ---------------- Helpers ----------------
        /// <summary>
        /// Returns true if the second grid coordinate is horizontally or vertically adjacent to the first.
        /// </summary>
        bool IsCardinalNeighbour(Vector2Int a, Vector2Int b)
        {
            var d = b - a;
            return (Mathf.Abs(d.x) == 1 && d.y == 0) || (Mathf.Abs(d.y) == 1 && d.x == 0);
        }

        bool PointerDownThisFrame()
        {
            if (Input.touchCount > 0) return Input.GetTouch(0).phase == TouchPhase.Began;
            return Input.GetMouseButtonDown(0);
        }
        bool PointerHeld()
        {
            if (Input.touchCount > 0)
            {
                var ph = Input.GetTouch(0).phase;
                return ph == TouchPhase.Moved || ph == TouchPhase.Stationary;
            }
            return Input.GetMouseButton(0);
        }
        bool PointerUpThisFrame()
        {
            if (Input.touchCount > 0)
            {
                var ph = Input.GetTouch(0).phase;
                return ph == TouchPhase.Ended || ph == TouchPhase.Canceled;
            }
            return Input.GetMouseButtonUp(0);
        }

        /// <summary>
        /// Converts the active pointer position into world space aligned with the grid plane.
        /// </summary>
        Vector3 PointerWorld()
        {
            Vector3 s = (Input.touchCount > 0) ? (Vector3)Input.GetTouch(0).position : Input.mousePosition;
            var w = cam ? cam.ScreenToWorldPoint(s) : s;
            w.z = grid ? grid.dotsPlaneZ : 0f; // project to grid plane
            return w;
        }

        // ---------------- Editor-only SAVE buttons (show in Play mode Game view) ----------------
#if UNITY_EDITOR
        [Header("Editor Save (Play Mode)")]
        public string levelAssetName = "New Level";  // Leave empty to use scene name

        void OnGUI()
        {
            // Draw simple UI in Game view (Editor only)
            const float pad = 10f;
            const float gap = 20f; // Gap between left and right boxes
            const float boxWidth = 280f;
            const float boxHeight = 120f;
            const float statusBoxWidth = 500f;
            const float statusBoxHeight = 60f;

            // Left side GUI box
            float leftX = pad;
            GUILayout.BeginArea(new Rect(leftX, pad, boxWidth, boxHeight), GUI.skin.box);
            GUILayout.Label("Runtime Painter (Editor Only Save)");

            // Undo button
            GUILayout.BeginHorizontal();
            GUI.enabled = createdArrows.Count > 0;
            if (GUILayout.Button($"↶ Undo ({createdArrows.Count})"))
                UndoLastArrow();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (GUILayout.Button("💾 Save Arrows as Level Asset"))
                SaveArrowsAsLevel();

            // Screenshot import feature (only if enabled)
            if (enableScreenshotImport)
            {
                GUILayout.Space(5);
                if (GUILayout.Button("📷 Load Screenshot"))
                    LoadAndProcessScreenshot();

                if (screenshotArrows.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button($"✓ Confirm ({screenshotArrows.Count} arrows)"))
                        ConfirmScreenshotArrows();
                    if (GUILayout.Button("✗ Clear Detected"))
                        ClearScreenshotArrows();
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndArea();

            // Right side GUI - Status label in a box
            // Position it after the left box with a gap, but ensure it doesn't go off-screen
            float rightX = leftX + boxWidth + gap;
            float maxRightX = Screen.width - statusBoxWidth - pad;
            // Use the smaller value to ensure it doesn't overlap and doesn't go off-screen
            rightX = Mathf.Min(rightX, maxRightX);

            GUILayout.BeginArea(new Rect(rightX, pad, statusBoxWidth, statusBoxHeight), GUI.skin.box);
            GUILayout.Label("Status:");
            GUILayout.Label(status);
            GUILayout.EndArea();
        }

        /// <summary>
        /// Ensures the provided Unity project folder path exists, creating any missing segments.
        /// </summary>
        void EnsureFolderExists(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                // Create nested folders if needed
                var parts = folder.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }
        }

        /// <summary>
        /// Serialises all active arrow lines into a reusable level asset for the editor workflow.
        /// </summary>
        public void SaveArrowsAsLevel()
        {
            string levelsFolderPath = "Assets/MultiTech Studio/Escape Game/Levels";
            EnsureFolderExists(levelsFolderPath);

            // Collect all ArrowLine instances in scene
            var arrows = FindObjectsByType<ArrowLine>(FindObjectsSortMode.None);
            if (arrows.Length == 0)
            {
                Debug.LogWarning("No ArrowLine objects to save.");
                return;
            }

            if (!grid)
            {
                Debug.LogWarning("No GridManager assigned.");
                return;
            }

            // Determine level name
            string levelName = string.IsNullOrEmpty(levelAssetName)
                ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                : levelAssetName;

            string assetPath = $"{levelsFolderPath}/{levelName}.asset";

            LevelAsset levelAsset = AssetDatabase.LoadAssetAtPath<LevelAsset>(assetPath);
            if (levelAsset == null)
            {
                levelAsset = ScriptableObject.CreateInstance<LevelAsset>();
                AssetDatabase.CreateAsset(levelAsset, assetPath);
            }

            // Populate level name
            levelAsset.levelName = levelName;

            // Capture camera size
            var mainCam = Camera.main;
            if (mainCam != null && mainCam.orthographic)
            {
                levelAsset.camSize = mainCam.orthographicSize;
            }
            else
            {
                Debug.LogWarning("Main Camera not found or not orthographic. Using default camSize.");
            }

            // Populate grid data
            levelAsset.grid = new GridDef
            {
                width = grid.width,
                height = grid.height,
                spacing = grid.spacing,
                origin = grid.origin
            };

            // Populate arrows data
            levelAsset.arrows.Clear();
            int totalPathPoints = 0;

            foreach (var arrow in arrows)
            {
                if (arrow.nodes == null || arrow.nodes.Count == 0) continue;

                var arrowDef = new ArrowLineDef
                {
                    path = new List<Vector2Int>(),
                    stepTime = arrow.stepTime,
                    zOffset = arrow.zOffset,
                    occupyAllNodes = arrow.occupyAllNodes,
                    headZ = arrow.headZ,
                    lineColor = GetLineColor(arrow),
                    hitColor = arrow.hitColor,
                    startIndex = arrow.StartIndexPublic
                };

                // Convert Dot list to Vector2Int grid coordinates
                foreach (var dot in arrow.nodes)
                {
                    if (dot != null)
                    {
                        arrowDef.path.Add(dot.G);
                    }
                }

                totalPathPoints += arrowDef.path.Count;
                levelAsset.arrows.Add(arrowDef);
            }

            // Populate meta info
            levelAsset.arrowCount = levelAsset.arrows.Count;
            levelAsset.dotsCount = grid.width * grid.height;
            levelAsset.totalPathPoints = totalPathPoints;

            // Save the asset
            EditorUtility.SetDirty(levelAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Saved level asset at: {assetPath}");
            Selection.activeObject = levelAsset;
        }

        // Helper method to get line color from ArrowLine
        /// <summary>
        /// Retrieves the primary colour from the line renderer attached to the supplied arrow.
        /// </summary>
        private Color GetLineColor(ArrowLine arrow)
        {
            var lr = arrow.GetComponent<LineRenderer>();
            if (lr != null)
            {
                if (lr.material.HasProperty("_Color"))
                    return lr.material.color;
                else
                    return lr.startColor;
            }
            return Color.black;
        }

        // ---------------- Screenshot Import Feature ----------------
        public RawImage targetRawImage;
        /// <summary>
        /// Opens file dialog to load a screenshot image and processes it to detect arrow paths.
        /// </summary>
        void LoadAndProcessScreenshot()
        {
            if (!grid || !arrowLinePrefab)
            {
                Debug.LogWarning("GridManager or ArrowLinePrefab not assigned.");
                return;
            }

            string path = EditorUtility.OpenFilePanel("Select Screenshot Image", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path))
                return;

            // Load image file
            byte[] fileData = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (!tex.LoadImage(fileData))
            {
                Debug.LogError("Failed to load image file.");
                return;
            }

            Debug.Log($"Processing screenshot: {tex.width}x{tex.height}");
            ProcessScreenshotToArrows(tex);
        }

        /// <summary>
        /// Processes a loaded texture to detect arrows and create ArrowLine objects.
        /// </summary>
        void ProcessScreenshotToArrows(Texture2D image)
        {
            ClearScreenshotArrows();

            // Convert to readable texture if needed
            Texture2D readableTex = MakeReadable(image);
            Show(readableTex);
            if (readableTex == null)
            {
                Debug.LogError("Failed to make texture readable.");
                return;
            }

            // Set grid dimensions (manual or auto-detect)
            if (useManualGridSize)
            {
                Debug.Log($"Using manual grid size: {manualGridWidth}x{manualGridHeight}");
                grid.width = manualGridWidth;
                grid.height = manualGridHeight;
                grid.RebuildSmart();
                Debug.Log($"Grid rebuilt with manual dimensions: {grid.width}x{grid.height}");
            }
            else
            {
                // Auto-detect grid dimensions from the image
                var gridSize = DetectGridDimensions(readableTex);
                if (gridSize.x > 0 && gridSize.y > 0)
                {
                    Debug.Log($"Detected grid size: {gridSize.x}x{gridSize.y} from image");
                    grid.width = gridSize.x;
                    grid.height = gridSize.y;
                    grid.RebuildSmart();
                    Debug.Log($"Grid rebuilt with dimensions: {grid.width}x{grid.height}");
                }
                else
                {
                    Debug.LogWarning("Could not detect grid dimensions from image. Using current grid size.");
                }
            }

            // Detect arrows and paths
            var detectedPaths = DetectArrowsAndPaths(readableTex);
            Debug.Log($"Detected {detectedPaths.Count} arrow paths from screenshot.");

            // Filter out overlapping and duplicate paths
            var filteredPaths = FilterOverlappingPaths(detectedPaths);
            Debug.Log($"After filtering overlaps: {filteredPaths.Count} unique paths remaining.");

            // Create ArrowLine objects from filtered paths
            foreach (var path in filteredPaths)
            {
                if (path.Count < 2) continue;

                var arrowLine = CreateArrowLineFromPath(path);
                if (arrowLine != null)
                {
                    screenshotArrows.Add(arrowLine);
                }
            }

            detectedArrowCount = screenshotArrows.Count;
            status += $" | Screenshot: {detectedArrowCount} arrows detected";

            // Cleanup
            if (readableTex != image)
                DestroyImmediate(readableTex);

            Debug.Log($"Created {detectedArrowCount} arrow lines from screenshot.");
        }

        /// <summary>
        /// Makes a texture readable by creating a copy if necessary.
        /// </summary>
        Texture2D MakeReadable(Texture2D source)
        {
            if (source.isReadable)
                return source;

            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readable = new Texture2D(source.width, source.height);
            readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }
        public void Show(Texture2D tex)
        {
            if (tex == null) return;
            targetRawImage.texture = tex;
            // optional: adjust rect transform to natural size
            targetRawImage.rectTransform.sizeDelta = new Vector2(tex.width, tex.height);
        }

        /// <summary>
        /// Detects arrows and traces paths through the maze image.
        /// Returns a list of paths, where each path is a list of grid coordinates.
        /// </summary>
        List<List<Vector2Int>> DetectArrowsAndPaths(Texture2D image)
        {
            var allPaths = new List<List<Vector2Int>>();
            if (image == null || grid == null || grid.width <= 0 || grid.height <= 0) return allPaths;

            int gridW = grid.width;
            int gridH = grid.height;

            // 1) Build boolean occupancy grid per cell (true = stroke present)
            bool[,] cellStroke = new bool[gridW, gridH];
            float cellW = image.width / (float)gridW;
            float cellH = image.height / (float)gridH;
            float insetPct = 0.18f; // avoid borders
            float baseThreshold = 0.45f;

            for (int gy = 0; gy < gridH; gy++)
            {
                for (int gx = 0; gx < gridW; gx++)
                {
                    float avg = SampleCellAverageGrayscale(image, gx, gy, cellW, cellH, insetPct);
                    bool isStroke = invertColors ? avg > (1f - baseThreshold) : avg < baseThreshold;
                    cellStroke[gx, gy] = isStroke;
                }
            }

            // 2) Cluster connected stroke cells into components (4-neighbour)
            bool[,] visited = new bool[gridW, gridH];
            for (int y = 0; y < gridH; y++)
            {
                for (int x = 0; x < gridW; x++)
                {
                    if (!cellStroke[x, y] || visited[x, y]) continue;
                    // flood fill component
                    var comp = new List<Vector2Int>();
                    var stack = new Stack<Vector2Int>();
                    stack.Push(new Vector2Int(x, y));
                    visited[x, y] = true;
                    while (stack.Count > 0)
                    {
                        var p = stack.Pop();
                        comp.Add(p);
                        foreach (var n in Get4Neighbours(p, gridW, gridH))
                        {
                            if (!visited[n.x, n.y] && cellStroke[n.x, n.y])
                            {
                                visited[n.x, n.y] = true;
                                stack.Push(n);
                            }
                        }
                    }

                    if (comp.Count < 2) continue; // ignore tiny specks

                    // 3) Trace an ordered polyline for this component
                    var poly = TraceComponentPolyline(comp, cellStroke, gridW, gridH, image, cellW, cellH, insetPct, baseThreshold);
                    if (poly == null || poly.Count < 2) continue;

                    // 4) Find arrowhead cell inside component (if any) and orient poly accordingly
                    Vector2Int? arrowCell = FindArrowHeadCellInComponent(image, comp, cellW, cellH, insetPct, baseThreshold);
                    if (arrowCell.HasValue)
                    {
                        // ensure poly goes from tail -> head
                        if (poly[poly.Count - 1] != arrowCell.Value && poly[0] == arrowCell.Value)
                        {
                            poly.Reverse();
                        }
                        else if (poly[poly.Count - 1] == arrowCell.Value)
                        {
                            // already tail->head
                        }
                        else
                        {
                            // arrow head cell is somewhere in middle — orient so head nearer to poly end
                            // compute distances
                            int distToStart = IndexOfCell(poly, arrowCell.Value);
                            if (distToStart >= 0)
                            {
                                // If the index is closer to start, reverse so arrow head at end
                                if (distToStart < poly.Count / 2)
                                    poly.Reverse();
                            }
                        }
                    }
                    else
                    {
                        // no arrowhead detected: try to pick endpoints to orient poly (endpoints -> head)
                        // prefer longer end as head (no-op), nothing to do
                    }

                    allPaths.Add(poly);
                }
            }

            Debug.Log($"Detected {allPaths.Count} components -> polyline paths from screenshot.");
            return allPaths;
        }
        // sample average grayscale inside central rectangle of a cell
        float SampleCellAverageGrayscale(Texture2D img, int gx, int gy, float cellW, float cellH, float insetPct)
        {
            int imgW = img.width, imgH = img.height;
            int sx = Mathf.RoundToInt(gx * cellW);
            int sy = Mathf.RoundToInt(gy * cellH);
            int ex = Mathf.RoundToInt((gx + 1) * cellW) - 1;
            int ey = Mathf.RoundToInt((gy + 1) * cellH) - 1;

            int w = Mathf.Max(1, ex - sx + 1);
            int h = Mathf.Max(1, ey - sy + 1);
            int ix = Mathf.RoundToInt(w * insetPct);
            int iy = Mathf.RoundToInt(h * insetPct);

            int ax = Mathf.Clamp(sx + ix, 0, imgW - 1);
            int ay = Mathf.Clamp(sy + iy, 0, imgH - 1);
            int bx = Mathf.Clamp(ex - ix, 0, imgW - 1);
            int by = Mathf.Clamp(ey - iy, 0, imgH - 1);

            int sampleCols = Mathf.Clamp(bx - ax + 1, 1, 7);
            int sampleRows = Mathf.Clamp(by - ay + 1, 1, 7);
            float sum = 0f; int cnt = 0;
            for (int ry = 0; ry < sampleRows; ry++)
            {
                int py = Mathf.Clamp(ax == bx ? ay : (ay + (ry * (by - ay) / Mathf.Max(1, sampleRows - 1))), 0, imgH - 1);
                for (int rx = 0; rx < sampleCols; rx++)
                {
                    int px = Mathf.Clamp(ay == by ? ax : (ax + (rx * (bx - ax) / Mathf.Max(1, sampleCols - 1))), 0, imgW - 1);
                    // NOTE: px/py above are swapped if my quick variable mapping misplaced; fix below to sample correctly:
                    px = Mathf.Clamp(ax + (rx * (bx - ax) / Mathf.Max(1, sampleCols - 1)), 0, imgW - 1);
                    py = Mathf.Clamp(ay + (ry * (by - ay) / Mathf.Max(1, sampleRows - 1)), 0, imgH - 1);

                    sum += img.GetPixel(px, py).grayscale;
                    cnt++;
                }
            }
            return cnt > 0 ? (sum / cnt) : 1f;
        }

        // returns 4-neighbours in bounds
        List<Vector2Int> Get4Neighbours(Vector2Int p, int gridW, int gridH)
        {
            var list = new List<Vector2Int>(4);
            if (p.x > 0) list.Add(new Vector2Int(p.x - 1, p.y));
            if (p.x < gridW - 1) list.Add(new Vector2Int(p.x + 1, p.y));
            if (p.y > 0) list.Add(new Vector2Int(p.x, p.y - 1));
            if (p.y < gridH - 1) list.Add(new Vector2Int(p.x, p.y + 1));
            return list;
        }

        // Trace an ordered polyline from a component set of cells
        List<Vector2Int> TraceComponentPolyline(List<Vector2Int> comp, bool[,] strokeGrid, int gridW, int gridH, Texture2D img, float cellW, float cellH, float insetPct, float baseThreshold)
        {
            var compSet = new HashSet<Vector2Int>(comp);
            // Build degree map
            Dictionary<Vector2Int, int> deg = new();
            foreach (var c in comp)
            {
                int d = 0;
                foreach (var n in Get4Neighbours(c, gridW, gridH))
                    if (compSet.Contains(n)) d++;
                deg[c] = d;
            }

            // Find endpoints (degree==1). If none (loop), pick a cell with minimal degree as start.
            Vector2Int start = new Vector2Int(-1, -1);
            foreach (var kv in deg)
            {
                if (kv.Value == 1) { start = kv.Key; break; }
            }
            if (start.x == -1)
            {
                // no endpoint - pick a cell with smallest degree (>0)
                int minD = int.MaxValue;
                foreach (var kv in deg)
                {
                    if (kv.Value < minD) { minD = kv.Value; start = kv.Key; }
                }
            }

            if (start.x == -1) return null;

            // Walk, always pick next unvisited neighbour cell within component
            var path = new List<Vector2Int> { start };
            var visited = new HashSet<Vector2Int> { start };
            Vector2Int cur = start;
            while (true)
            {
                Vector2Int next = new Vector2Int(-1, -1);
                foreach (var n in Get4Neighbours(cur, gridW, gridH))
                {
                    if (compSet.Contains(n) && !visited.Contains(n))
                    {
                        // require actual pixel connection between cur and n
                        if (IsConnected(img, cur, n, cellW, cellH, insetPct, baseThreshold))
                        {
                            next = n;
                            break;
                        }
                    }
                }
                if (next.x == -1) break;
                path.Add(next);
                visited.Add(next);
                cur = next;
                if (path.Count > comp.Count + 5) break; // safety
            }

            return path.Count >= 2 ? path : null;
        }
        /// <summary>
        /// Returns true if there is a visible stroke connecting two adjacent grid cells (cardinal).
        /// Samples a small strip across the midline between the two cells and averages grayscale.
        /// </summary>
        bool IsConnected(Texture2D img, Vector2Int a, Vector2Int b, float cellW, float cellH, float insetPct, float threshold)
        {
            // only cardinal neighbors allowed
            var diff = b - a;
            if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) != 1) return false;

            int imgW = img.width, imgH = img.height;

            // compute real world pixel center for each cell (Texture2D: y=0 bottom)
            float axf = (a.x + 0.5f) * cellW;
            float ayf = (a.y + 0.5f) * cellH;
            float bxf = (b.x + 0.5f) * cellW;
            float byf = (b.y + 0.5f) * cellH;

            // midline center point (in pixels)
            float mx = (axf + bxf) * 0.5f;
            float my = (ayf + byf) * 0.5f;

            // define sampling rectangle oriented along the edge:
            // If neighbor is vertical (diff.x != 0): sample a vertical strip centered at mx,my
            // If neighbor is horizontal (diff.y != 0): sample a horizontal strip

            int sampleHalfW = Mathf.Max(1, Mathf.RoundToInt((cellW * 0.15f))); // thickness of strip across edge
            int sampleHalfH = Mathf.Max(1, Mathf.RoundToInt((cellH * 0.15f)));

            int sx, ex, sy, ey;

            if (diff.x != 0) // left/right neighbor -> vertical strip
            {
                sx = Mathf.Clamp(Mathf.RoundToInt(mx) - sampleHalfW, 0, imgW - 1);
                ex = Mathf.Clamp(Mathf.RoundToInt(mx) + sampleHalfW, 0, imgW - 1);
                // sample along small vertical range around mid Y (cover 40% of cell height)
                sy = Mathf.Clamp(Mathf.RoundToInt(my - cellH * 0.25f), 0, imgH - 1);
                ey = Mathf.Clamp(Mathf.RoundToInt(my + cellH * 0.25f), 0, imgH - 1);
            }
            else // up/down neighbor -> horizontal strip
            {
                sy = Mathf.Clamp(Mathf.RoundToInt(my) - sampleHalfH, 0, imgH - 1);
                ey = Mathf.Clamp(Mathf.RoundToInt(my) + sampleHalfH, 0, imgH - 1);
                // sample along small horizontal range around mid X (cover 40% of cell width)
                sx = Mathf.Clamp(Mathf.RoundToInt(mx - cellW * 0.25f), 0, imgW - 1);
                ex = Mathf.Clamp(Mathf.RoundToInt(mx + cellW * 0.25f), 0, imgW - 1);
            }

            // sample a grid of pixels inside this rectangle (limit samples for speed)
            int cols = Mathf.Clamp(ex - sx + 1, 1, 9);
            int rows = Mathf.Clamp(ey - sy + 1, 1, 9);
            float sum = 0f; int cnt = 0;
            for (int ry = 0; ry < rows; ry++)
            {
                int py = Mathf.Clamp(sy + (ry * (ey - sy) / Mathf.Max(1, rows - 1)), 0, imgH - 1);
                for (int cx = 0; cx < cols; cx++)
                {
                    int px = Mathf.Clamp(sx + (cx * (ex - sx) / Mathf.Max(1, cols - 1)), 0, imgW - 1);
                    sum += img.GetPixel(px, py).grayscale;
                    cnt++;
                }
            }
            if (cnt == 0) return false;
            float avg = sum / cnt;
            // stroke if avg is dark (respect invertColors)
            bool stroke = invertColors ? avg > (1f - threshold) : avg < threshold;
            return stroke;
        }


        // Find any cell inside the component that looks like an arrow head (samples from cell edges)
        Vector2Int? FindArrowHeadCellInComponent(Texture2D img, List<Vector2Int> comp, float cellW, float cellH, float insetPct, float baseThreshold)
        {
            Vector2Int? best = null;
            float bestScore = float.MaxValue;

            foreach (var cell in comp)
            {
                // detect direction presence and arrowhead shape by sampling corners/sides
                Dir d = DetectDirectionInCell(img, cell.x, cell.y, cellW, cellH, insetPct, baseThreshold);
                if (d == Dir.None) continue;

                // Score: prefer darker center near the sampled head side (lower grayscale better)
                // compute head sample point (approx)
                int imgW = img.width, imgH = img.height;
                int cx = Mathf.Clamp(Mathf.RoundToInt((cell.x + 0.5f) * cellW), 0, imgW - 1);
                int cy = Mathf.Clamp(Mathf.RoundToInt((cell.y + 0.5f) * cellH), 0, imgH - 1);

                int hx = cx, hy = cy;
                switch (d)
                {
                    case Dir.Up: hy = Mathf.Clamp(Mathf.RoundToInt((cell.y + 0.85f) * cellH), 0, imgH - 1); break;
                    case Dir.Down: hy = Mathf.Clamp(Mathf.RoundToInt((cell.y + 0.15f) * cellH), 0, imgH - 1); break;
                    case Dir.Left: hx = Mathf.Clamp(Mathf.RoundToInt((cell.x + 0.15f) * cellW), 0, imgW - 1); break;
                    case Dir.Right: hx = Mathf.Clamp(Mathf.RoundToInt((cell.x + 0.85f) * cellW), 0, imgW - 1); break;
                }

                float val = img.GetPixel(hx, hy).grayscale;
                // lower val -> more likely head (dark)
                if (invertColors) val = 1f - val;

                if (val < bestScore)
                {
                    bestScore = val;
                    best = cell;
                }
            }

            return best;
        }

        // index helper
        int IndexOfCell(List<Vector2Int> list, Vector2Int cell)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == cell) return i;
            return -1;
        }

        // Detect direction in a cell (same helper as previous but ensured available)
        Dir DetectDirectionInCell(Texture2D img, int gx, int gy, float cellW, float cellH, float insetPct, float threshold)
        {
            int imgW = img.width, imgH = img.height;
            int cx = Mathf.Clamp(Mathf.RoundToInt((gx + 0.5f) * cellW), 0, imgW - 1);
            int cy = Mathf.Clamp(Mathf.RoundToInt((gy + 0.5f) * cellH), 0, imgH - 1);

            int topY = Mathf.Clamp(Mathf.RoundToInt((gy + 0.85f) * cellH), 0, imgH - 1);
            int botY = Mathf.Clamp(Mathf.RoundToInt((gy + 0.15f) * cellH), 0, imgH - 1);
            int leftX = Mathf.Clamp(Mathf.RoundToInt((gx + 0.15f) * cellW), 0, imgW - 1);
            int rightX = Mathf.Clamp(Mathf.RoundToInt((gx + 0.85f) * cellW), 0, imgW - 1);

            float top = img.GetPixel(cx, topY).grayscale;
            float bot = img.GetPixel(cx, botY).grayscale;
            float left = img.GetPixel(leftX, cy).grayscale;
            float right = img.GetPixel(rightX, cy).grayscale;

            float th = threshold;
            bool topDark = invertColors ? top > (1f - th) : top < th;
            bool botDark = invertColors ? bot > (1f - th) : bot < th;
            bool leftDark = invertColors ? left > (1f - th) : left < th;
            bool rightDark = invertColors ? right > (1f - th) : right < th;

            if (topDark && !botDark && !leftDark && !rightDark) return Dir.Up;
            if (botDark && !topDark && !leftDark && !rightDark) return Dir.Down;
            if (leftDark && !rightDark && !topDark && !botDark) return Dir.Left;
            if (rightDark && !leftDark && !topDark && !botDark) return Dir.Right;

            // fallback: choose min
            float min = Mathf.Min(top, bot, left, right);
            if (Mathf.Approximately(min, top)) return Dir.Up;
            if (Mathf.Approximately(min, bot)) return Dir.Down;
            if (Mathf.Approximately(min, left)) return Dir.Left;
            if (Mathf.Approximately(min, right)) return Dir.Right;
            return Dir.None;
        }

        /// <summary>
        /// Calculates optimal threshold for black/white separation in the image.
        /// </summary>
        float CalculateOptimalThreshold(Texture2D image)
        {
            int width = image.width;
            int height = image.height;

            // Sample image to build histogram
            List<float> grayValues = new List<float>();
            int sampleStep = Mathf.Max(1, width / 100);

            for (int y = 0; y < height; y += sampleStep)
            {
                for (int x = 0; x < width; x += sampleStep)
                {
                    grayValues.Add(image.GetPixel(x, y).grayscale);
                }
            }

            if (grayValues.Count == 0) return 0.5f;

            // Sort and find a good threshold (typically between darkest and lightest values)
            grayValues.Sort();

            // Use Otsu's method approximation: find valley between two peaks
            // For maze images, we typically have a bimodal distribution (black lines, white paths)
            float median = grayValues[grayValues.Count / 2];

            // Adjust threshold: for clean mazes, median works well
            // For noisy images, use slightly higher threshold
            return Mathf.Clamp(median, 0.3f, 0.7f);
        }

        /// <summary>
        /// Counts the number of arrow pixels detected.
        /// </summary>
        int CountArrows(bool[,] isArrow, int width, int height)
        {
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (isArrow[x, y]) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Filters out overlapping and duplicate paths.
        /// Keeps only unique, non-overlapping arrow paths.
        /// </summary>
        List<List<Vector2Int>> FilterOverlappingPaths(List<List<Vector2Int>> paths)
        {
            if (paths.Count == 0) return paths;

            var filtered = new List<List<Vector2Int>>();
            var pathSets = new List<HashSet<Vector2Int>>();

            // Sort paths by length (longer paths first) to keep more complete paths
            var sortedPaths = new List<List<Vector2Int>>(paths);
            sortedPaths.Sort((a, b) => b.Count.CompareTo(a.Count));

            foreach (var path in sortedPaths)
            {
                if (path.Count < 2) continue;

                // Clean the path first
                var cleaned = CleanPath(path);
                if (cleaned.Count < 2) continue;

                // Check for self-loops (path that goes back to a previous position)
                if (HasSelfLoop(cleaned))
                {
                    Debug.Log($"Skipping path with self-loop: {string.Join(" -> ", cleaned)}");
                    continue;
                }

                // Convert to set for overlap checking
                var pathSet = new HashSet<Vector2Int>(cleaned);

                // Check overlap with existing filtered paths
                bool isDuplicate = false;
                float maxOverlap = 0f;

                foreach (var existingSet in pathSets)
                {
                    float overlapRatio = CalculateOverlapRatio(pathSet, existingSet);
                    maxOverlap = Mathf.Max(maxOverlap, overlapRatio);

                    // If more than 60% overlap, consider it a duplicate (stricter threshold)
                    if (overlapRatio > 0.6f)
                    {
                        isDuplicate = true;
                        Debug.Log($"Skipping duplicate path (overlap: {overlapRatio:P0}): {string.Join(" -> ", cleaned)}");
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    filtered.Add(cleaned);
                    pathSets.Add(pathSet);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Checks if a path has a self-loop (returns to a previously visited position).
        /// </summary>
        bool HasSelfLoop(List<Vector2Int> path)
        {
            if (path.Count < 3) return false;

            var visited = new HashSet<Vector2Int>();
            foreach (var pos in path)
            {
                if (visited.Contains(pos))
                {
                    // Allow immediate backtrack (A->B->A) but not longer loops
                    int index = path.IndexOf(pos);
                    if (index > 0 && path[index - 1] == pos)
                        continue; // Immediate repeat is OK (could be path sampling artifact)
                    return true; // True loop detected
                }
                visited.Add(pos);
            }
            return false;
        }

        /// <summary>
        /// Calculates the overlap ratio between two paths.
        /// Returns a value between 0 (no overlap) and 1 (complete overlap).
        /// </summary>
        float CalculateOverlapRatio(HashSet<Vector2Int> path1, HashSet<Vector2Int> path2)
        {
            if (path1.Count == 0 || path2.Count == 0) return 0f;

            int overlapCount = 0;
            foreach (var pos in path1)
            {
                if (path2.Contains(pos))
                    overlapCount++;
            }

            // Use the smaller path as denominator (more strict)
            int minSize = Mathf.Min(path1.Count, path2.Count);
            return overlapCount / (float)minSize;
        }

        /// <summary>
        /// Cleans a path by removing duplicates, loops, and ensuring only cardinal neighbor connections.
        /// </summary>
        List<Vector2Int> CleanPath(List<Vector2Int> path)
        {
            if (path.Count < 2) return path;

            var cleaned = new List<Vector2Int>();
            cleaned.Add(path[0]);
            var visitedInPath = new HashSet<Vector2Int> { path[0] };

            for (int i = 1; i < path.Count; i++)
            {
                var current = path[i];
                var last = cleaned[cleaned.Count - 1];

                // Skip if same as last point
                if (current == last) continue;

                // Check if we're going back to a previously visited position (loop)
                if (visitedInPath.Contains(current) && current != last)
                {
                    // Allow immediate backtrack (A->B->A) but stop at longer loops
                    int lastIndex = cleaned.Count - 1;
                    if (lastIndex > 0 && cleaned[lastIndex - 1] == current)
                    {
                        // Immediate backtrack - remove the last point
                        cleaned.RemoveAt(lastIndex);
                        visitedInPath.Remove(last);
                        continue;
                    }
                    else
                    {
                        // Longer loop detected - stop here
                        break;
                    }
                }

                // Check if it's a cardinal neighbor
                var diff = current - last;
                if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) == 1)
                {
                    // Direct neighbor, add it
                    cleaned.Add(current);
                    visitedInPath.Add(current);
                }
                else if (Mathf.Abs(diff.x) <= 1 && Mathf.Abs(diff.y) <= 1)
                {
                    // Diagonal neighbor, skip (not allowed in grid)
                    continue;
                }
                else
                {
                    // Gap - fill in intermediate points if within reasonable distance
                    if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) <= 3)
                    {
                        // Fill in cardinal path
                        Vector2Int pos = last;
                        while (pos != current)
                        {
                            if (pos.x < current.x) pos.x++;
                            else if (pos.x > current.x) pos.x--;
                            else if (pos.y < current.y) pos.y++;
                            else if (pos.y > current.y) pos.y--;

                            if (pos != last && pos != current)
                            {
                                // Check for loops during interpolation
                                if (visitedInPath.Contains(pos))
                                    break;

                                cleaned.Add(pos);
                                visitedInPath.Add(pos);
                            }
                        }

                        if (!visitedInPath.Contains(current))
                        {
                            cleaned.Add(current);
                            visitedInPath.Add(current);
                        }
                    }
                    else
                    {
                        // Gap too large, stop path here
                        break;
                    }
                }
            }

            // Final cleanup: remove any remaining immediate duplicates
            var finalCleaned = new List<Vector2Int>();
            for (int i = 0; i < cleaned.Count; i++)
            {
                if (finalCleaned.Count == 0 || finalCleaned[finalCleaned.Count - 1] != cleaned[i])
                {
                    finalCleaned.Add(cleaned[i]);
                }
            }

            return finalCleaned.Count >= 2 ? finalCleaned : new List<Vector2Int>();
        }

        /// <summary>
        /// Detects arrow direction at a pixel by checking surrounding pattern.
        /// Improved to better detect arrow shaft and point direction.
        /// </summary>
        Dir DetectArrowDirection(Texture2D image, int x, int y, float threshold)
        {
            int width = image.width;
            int height = image.height;
            int checkRadius = 6;

            // Method 1: Check for arrow shaft (line of black pixels extending in one direction)
            int upExtent = 0, downExtent = 0, leftExtent = 0, rightExtent = 0;

            // Check how far the black pixels extend in each direction
            for (int i = 1; i <= checkRadius; i++)
            {
                if (y - i >= 0 && image.GetPixel(x, y - i).grayscale <= threshold)
                    upExtent = i;
                else
                    break;
            }
            for (int i = 1; i <= checkRadius; i++)
            {
                if (y + i < height && image.GetPixel(x, y + i).grayscale <= threshold)
                    downExtent = i;
                else
                    break;
            }
            for (int i = 1; i <= checkRadius; i++)
            {
                if (x - i >= 0 && image.GetPixel(x - i, y).grayscale <= threshold)
                    leftExtent = i;
                else
                    break;
            }
            for (int i = 1; i <= checkRadius; i++)
            {
                if (x + i < width && image.GetPixel(x + i, y).grayscale <= threshold)
                    rightExtent = i;
                else
                    break;
            }

            // Method 2: Check for arrowhead point (fewer black pixels in one direction)
            // The arrow points in the direction with LESS extension (the point)
            // The shaft is in the opposite direction (MORE extension)

            // If we have a clear asymmetry, the arrow points toward the shorter side
            int maxExtent = Mathf.Max(Mathf.Max(upExtent, downExtent), Mathf.Max(leftExtent, rightExtent));

            // Arrow points toward the side with less black pixels (the point)
            if (downExtent >= 2 && upExtent < downExtent / 2) return Dir.Up;
            if (upExtent >= 2 && downExtent < upExtent / 2) return Dir.Down;
            if (rightExtent >= 2 && leftExtent < rightExtent / 2) return Dir.Left;
            if (leftExtent >= 2 && rightExtent < leftExtent / 2) return Dir.Right;

            // Fallback: use the direction with the most extent (shaft direction)
            // Then arrow points opposite to that
            if (upExtent == maxExtent && maxExtent >= 2) return Dir.Down; // Shaft goes up, arrow points down
            if (downExtent == maxExtent && maxExtent >= 2) return Dir.Up; // Shaft goes down, arrow points up
            if (leftExtent == maxExtent && maxExtent >= 2) return Dir.Right; // Shaft goes left, arrow points right
            if (rightExtent == maxExtent && maxExtent >= 2) return Dir.Left; // Shaft goes right, arrow points left

            return Dir.Right; // Default fallback
        }

        /// <summary>
        /// Checks if a pixel is likely an arrowhead (triangular shape).
        /// Improved to detect various arrow styles.
        /// </summary>
        bool IsArrowHead(Texture2D image, int x, int y, float threshold)
        {
            int width = image.width;
            int height = image.height;

            // Check for arrow-like patterns in all 4 directions
            // An arrow typically has:
            // 1. A concentration of black pixels forming a point
            // 2. Black pixels extending in one direction (shaft)

            // Count black pixels in each direction
            int upBlack = 0, downBlack = 0, leftBlack = 0, rightBlack = 0;
            int checkRadius = 5;

            for (int i = 1; i <= checkRadius; i++)
            {
                // Up
                if (y - i >= 0 && image.GetPixel(x, y - i).grayscale <= threshold)
                    upBlack++;
                // Down
                if (y + i < height && image.GetPixel(x, y + i).grayscale <= threshold)
                    downBlack++;
                // Left
                if (x - i >= 0 && image.GetPixel(x - i, y).grayscale <= threshold)
                    leftBlack++;
                // Right
                if (x + i < width && image.GetPixel(x + i, y).grayscale <= threshold)
                    rightBlack++;
            }

            // If we have a strong direction (3+ consecutive black pixels), it's likely an arrow
            int maxDirectional = Mathf.Max(Mathf.Max(upBlack, downBlack), Mathf.Max(leftBlack, rightBlack));

            // Also check for triangular pattern (wedge shape)
            int blackNeighbors = 0;
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    if (image.GetPixel(nx, ny).grayscale <= threshold)
                        blackNeighbors++;
                }
            }

            // Arrow detection criteria:
            // - Has directional extent (3+ black pixels in one direction)
            // - Has 5-20 black neighbors (forms a visible shape)
            return maxDirectional >= 3 && blackNeighbors >= 5 && blackNeighbors <= 20;
        }

        /// <summary>
        /// Detects grid dimensions by analyzing the image structure.
        /// Returns detected width and height in grid cells.
        /// </summary>
        Vector2Int DetectGridDimensions(Texture2D image)
        {
            int width = image.width;
            int height = image.height;
            float threshold = 0.5f;

            // Method 1: Detect by finding repeating patterns (grid cells)
            // Look for consistent spacing in path segments
            List<int> verticalSpacings = new List<int>();
            List<int> horizontalSpacings = new List<int>();

            // Sample vertical columns to find cell width
            int sampleStep = Mathf.Max(1, width / 100);
            int lastPathStart = -1;
            for (int x = 0; x < width; x += sampleStep)
            {
                // Find first path pixel in this column
                int pathStart = -1;
                for (int y = 0; y < height; y++)
                {
                    if (image.GetPixel(x, y).grayscale > threshold)
                    {
                        pathStart = y;
                        break;
                    }
                }

                if (pathStart >= 0 && lastPathStart >= 0)
                {
                    int spacing = x - lastPathStart;
                    if (spacing > 5 && spacing < width / 2) // Reasonable spacing
                    {
                        verticalSpacings.Add(spacing);
                    }
                }

                if (pathStart >= 0)
                    lastPathStart = x;
            }

            // Sample horizontal rows to find cell height
            lastPathStart = -1;
            for (int y = 0; y < height; y += sampleStep)
            {
                int pathStart = -1;
                for (int x = 0; x < width; x++)
                {
                    if (image.GetPixel(x, y).grayscale > threshold)
                    {
                        pathStart = x;
                        break;
                    }
                }

                if (pathStart >= 0 && lastPathStart >= 0)
                {
                    int spacing = y - lastPathStart;
                    if (spacing > 5 && spacing < height / 2)
                    {
                        horizontalSpacings.Add(spacing);
                    }
                }

                if (pathStart >= 0)
                    lastPathStart = y;
            }

            // Calculate average cell size
            int avgCellWidth = CalculateAverageSpacing(verticalSpacings);
            int avgCellHeight = CalculateAverageSpacing(horizontalSpacings);

            // Estimate grid dimensions
            int estimatedWidth = avgCellWidth > 0 ? Mathf.Max(5, width / avgCellWidth) : 0;
            int estimatedHeight = avgCellHeight > 0 ? Mathf.Max(5, height / avgCellHeight) : 0;

            if (estimatedWidth > 0 && estimatedHeight > 0)
            {
                Debug.Log($"Grid detected: {estimatedWidth}x{estimatedHeight} (cell size ~{avgCellWidth}x{avgCellHeight}px)");
                return new Vector2Int(estimatedWidth, estimatedHeight);
            }

            // Fallback: Estimate based on image size
            // Try to detect by analyzing path density
            int pathPixelCount = 0;
            for (int y = 0; y < height; y += 10)
            {
                for (int x = 0; x < width; x += 10)
                {
                    if (image.GetPixel(x, y).grayscale > threshold)
                        pathPixelCount++;
                }
            }

            float pathDensity = pathPixelCount / (float)((width / 10) * (height / 10));

            // If path density is low, it's likely a sparse grid
            // If high, it's a dense maze
            // Estimate cell size based on density
            int cellSizeEstimate = pathDensity > 0.3f ? 25 : 40;
            int fallbackWidth = Mathf.Max(5, Mathf.Min(50, width / cellSizeEstimate));
            int fallbackHeight = Mathf.Max(5, Mathf.Min(50, height / cellSizeEstimate));

            Debug.LogWarning($"Grid detection fallback: Using estimated {fallbackWidth}x{fallbackHeight} (path density: {pathDensity:F2})");
            return new Vector2Int(fallbackWidth, fallbackHeight);
        }

        /// <summary>
        /// Calculates the most common spacing value (mode) from a list of spacings.
        /// </summary>
        int CalculateAverageSpacing(List<int> spacings)
        {
            if (spacings.Count == 0) return 0;

            // Group similar spacings together (within 20% tolerance)
            Dictionary<int, int> spacingGroups = new Dictionary<int, int>();
            foreach (int spacing in spacings)
            {
                bool foundGroup = false;
                foreach (var key in spacingGroups.Keys)
                {
                    if (Mathf.Abs(spacing - key) <= key * 0.2f) // Within 20%
                    {
                        spacingGroups[key]++;
                        foundGroup = true;
                        break;
                    }
                }
                if (!foundGroup)
                {
                    spacingGroups[spacing] = 1;
                }
            }

            // Find the most common spacing
            int maxCount = 0;
            int bestSpacing = 0;
            foreach (var kvp in spacingGroups)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    bestSpacing = kvp.Key;
                }
            }

            return bestSpacing;
        }


        /// <summary>
        /// Arrow cluster data structure for grouping related arrow pixels.
        /// </summary>
        class ArrowCluster
        {
            public List<Vector2Int> pixels = new List<Vector2Int>();
            public Vector2Int centerPixel;
            public Dir direction;
        }

        /// <summary>
        /// Clusters arrow pixels into distinct arrow shapes using flood fill.
        /// </summary>
        List<ArrowCluster> ClusterArrowPixels(bool[,] isArrow, Dir[,] arrowDir, int width, int height)
        {
            var clusters = new List<ArrowCluster>();
            bool[,] clustered = new bool[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (isArrow[x, y] && !clustered[x, y])
                    {
                        // Start a new cluster
                        var cluster = new ArrowCluster();
                        FloodFillArrowCluster(x, y, isArrow, arrowDir, clustered, cluster, width, height);

                        if (cluster.pixels.Count > 0)
                        {
                            // Calculate cluster center
                            int sumX = 0, sumY = 0;
                            foreach (var p in cluster.pixels)
                            {
                                sumX += p.x;
                                sumY += p.y;
                            }
                            cluster.centerPixel = new Vector2Int(sumX / cluster.pixels.Count, sumY / cluster.pixels.Count);
                            cluster.direction = arrowDir[cluster.centerPixel.x, cluster.centerPixel.y];

                            clusters.Add(cluster);
                        }
                    }
                }
            }

            return clusters;
        }

        /// <summary>
        /// Flood fill to find all pixels belonging to an arrow shape.
        /// </summary>
        void FloodFillArrowCluster(int x, int y, bool[,] isArrow, Dir[,] arrowDir, bool[,] clustered, ArrowCluster cluster, int width, int height)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            if (!isArrow[x, y] || clustered[x, y]) return;

            clustered[x, y] = true;
            cluster.pixels.Add(new Vector2Int(x, y));

            // Flood fill to neighbors
            FloodFillArrowCluster(x + 1, y, isArrow, arrowDir, clustered, cluster, width, height);
            FloodFillArrowCluster(x - 1, y, isArrow, arrowDir, clustered, cluster, width, height);
            FloodFillArrowCluster(x, y + 1, isArrow, arrowDir, clustered, cluster, width, height);
            FloodFillArrowCluster(x, y - 1, isArrow, arrowDir, clustered, cluster, width, height);
        }

        /// <summary>
        /// Traces a path starting from an arrow, following the direction and path pixels.
        /// Improved to handle curves by following actual path pixels, not just cardinal directions.
        /// </summary>
        List<Vector2Int> TracePathFromArrow(Texture2D image, int startX, int startY, bool[,] isPath, bool[,] isArrow, Dir[,] arrowDir, bool[,] visited, Dir initialDir)
        {
            var path = new List<Vector2Int>();
            int width = image.width;
            int height = image.height;

            // Map image coordinates to grid coordinates
            // Assume image covers the grid area - we'll need to scale/offset
            Vector2Int currentImg = new Vector2Int(startX, startY);
            Dir currentDir = initialDir;

            // Convert first point to grid
            Vector2Int gridPos = ImageToGrid(currentImg, width, height, image);
            path.Add(gridPos);
            visited[startX, startY] = true;

            // Follow the path - improved to handle curves
            int maxSteps = grid.width * grid.height * 4; // Safety limit (allow for longer curved paths)
            int steps = 0;
            Vector2Int lastGridPos = gridPos;
            int pixelsPerGridCell = Mathf.Max(1, Mathf.Min(width / grid.width, height / grid.height));
            int sampleInterval = Mathf.Max(1, pixelsPerGridCell / 2); // Sample more frequently for curves

            // Track path pixels for curve following
            List<Vector2Int> imagePathPixels = new List<Vector2Int>();
            imagePathPixels.Add(currentImg);

            while (steps < maxSteps)
            {
                // Find next path pixel by checking neighbors (allows curves, not just cardinal)
                Vector2Int nextImg = FindNextPathPixel(image, currentImg, currentDir, isPath, isArrow, visited, width, height);

                // If no valid next pixel found, try to continue in current direction
                if (nextImg == currentImg)
                {
                    // Try moving in current direction
                    nextImg = currentImg;
                    switch (currentDir)
                    {
                        case Dir.Up: nextImg.y += 1; break;
                        case Dir.Down: nextImg.y -= 1; break;
                        case Dir.Left: nextImg.x -= 1; break;
                        case Dir.Right: nextImg.x += 1; break;
                    }

                    // Check bounds
                    if (nextImg.x < 0 || nextImg.x >= width || nextImg.y < 0 || nextImg.y >= height)
                        break;

                    // Check if we hit a wall
                    if (!isPath[nextImg.x, nextImg.y] && !isArrow[nextImg.x, nextImg.y])
                        break;
                }

                currentImg = nextImg;

                // Check for direction change (new arrow)
                if (isArrow[nextImg.x, nextImg.y] && arrowDir[nextImg.x, nextImg.y] != currentDir)
                {
                    currentDir = arrowDir[nextImg.x, nextImg.y];
                }

                // Add to image path for curve tracking
                if (!imagePathPixels.Contains(nextImg))
                {
                    imagePathPixels.Add(nextImg);
                }

                // Sample grid positions at intervals (for curves, we sample more frequently)
                if (imagePathPixels.Count % sampleInterval == 0 || isArrow[nextImg.x, nextImg.y])
                {
                    Vector2Int nextGrid = ImageToGrid(currentImg, width, height, image);
                    if (nextGrid != lastGridPos)
                    {
                        path.Add(nextGrid);
                        lastGridPos = nextGrid;
                    }
                }

                // Stop if we're looping in grid space (check last 5 positions to avoid false positives)
                if (path.Count > 5)
                {
                    int lastIndex = path.Count - 1;
                    // Check if we've returned to a recently visited grid position (within last 5)
                    int checkStart = Mathf.Max(0, lastIndex - 5);
                    for (int i = checkStart; i < lastIndex - 1; i++)
                    {
                        if (path[i] == lastGridPos)
                        {
                            // Loop detected, stop here
                            Debug.Log($"Loop detected in path tracing at {lastGridPos}, stopping.");
                            return path;
                        }
                    }
                }

                visited[nextImg.x, nextImg.y] = true;
                steps++;
            }

            // Final sample - ensure we capture the end point
            Vector2Int finalGrid = ImageToGrid(currentImg, width, height, image);
            if (path.Count == 0 || path[path.Count - 1] != finalGrid)
            {
                path.Add(finalGrid);
            }

            return path.Count >= 2 ? path : null;
        }

        /// <summary>
        /// Finds the next path pixel by checking neighboring pixels (allows curves).
        /// Checks in the current direction first, then adjacent directions.
        /// </summary>
        Vector2Int FindNextPathPixel(Texture2D image, Vector2Int current, Dir currentDir, bool[,] isPath, bool[,] isArrow, bool[,] visited, int width, int height)
        {
            // Priority order: forward, then left/right of forward, then backward
            Vector2Int[] directions = new Vector2Int[8];

            // Map current direction to pixel offsets
            switch (currentDir)
            {
                case Dir.Up:
                    directions[0] = new Vector2Int(0, 1);   // Forward
                    directions[1] = new Vector2Int(-1, 1); // Forward-left
                    directions[2] = new Vector2Int(1, 1);  // Forward-right
                    directions[3] = new Vector2Int(-1, 0); // Left
                    directions[4] = new Vector2Int(1, 0);  // Right
                    directions[5] = new Vector2Int(-1, -1); // Back-left
                    directions[6] = new Vector2Int(1, -1);  // Back-right
                    directions[7] = new Vector2Int(0, -1);  // Back
                    break;
                case Dir.Down:
                    directions[0] = new Vector2Int(0, -1);
                    directions[1] = new Vector2Int(1, -1);
                    directions[2] = new Vector2Int(-1, -1);
                    directions[3] = new Vector2Int(1, 0);
                    directions[4] = new Vector2Int(-1, 0);
                    directions[5] = new Vector2Int(1, 1);
                    directions[6] = new Vector2Int(-1, 1);
                    directions[7] = new Vector2Int(0, 1);
                    break;
                case Dir.Left:
                    directions[0] = new Vector2Int(-1, 0);
                    directions[1] = new Vector2Int(-1, 1);
                    directions[2] = new Vector2Int(-1, -1);
                    directions[3] = new Vector2Int(0, 1);
                    directions[4] = new Vector2Int(0, -1);
                    directions[5] = new Vector2Int(1, 1);
                    directions[6] = new Vector2Int(1, -1);
                    directions[7] = new Vector2Int(1, 0);
                    break;
                case Dir.Right:
                    directions[0] = new Vector2Int(1, 0);
                    directions[1] = new Vector2Int(1, -1);
                    directions[2] = new Vector2Int(1, 1);
                    directions[3] = new Vector2Int(0, -1);
                    directions[4] = new Vector2Int(0, 1);
                    directions[5] = new Vector2Int(-1, -1);
                    directions[6] = new Vector2Int(-1, 1);
                    directions[7] = new Vector2Int(-1, 0);
                    break;
            }

            // Check each direction in priority order
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = current + directions[i];

                // Check bounds
                if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height)
                    continue;

                // Check if it's a path or arrow pixel and not visited
                if ((isPath[next.x, next.y] || isArrow[next.x, next.y]) && !visited[next.x, next.y])
                {
                    return next;
                }
            }

            // If no unvisited path found, allow revisiting (for loops)
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = current + directions[i];
                if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height)
                    continue;

                if (isPath[next.x, next.y] || isArrow[next.x, next.y])
                {
                    return next;
                }
            }

            return current; // No valid next pixel found
        }

        /// <summary>
        /// Converts image pixel coordinates to grid coordinates by mapping image into exact grid cells.
        /// Uses averaging inside the cell to produce robust snapping.
        /// </summary>
        Vector2Int ImageToGrid(Vector2Int imagePos, int imageWidth, int imageHeight, Texture2D image)
        {
            // Defensive
            if (grid == null || grid.width <= 0 || grid.height <= 0)
                return Vector2Int.zero;

            // Map whole image area to grid (no arbitrary margins) so each cell = block
            float cellWf = imageWidth / (float)grid.width;
            float cellHf = imageHeight / (float)grid.height;

            // Determine which cell the pixel belongs to
            int cellX = Mathf.Clamp(Mathf.FloorToInt(imagePos.x / cellWf), 0, grid.width - 1);
            int cellY = Mathf.Clamp(Mathf.FloorToInt(imagePos.y / cellHf), 0, grid.height - 1);

            // Optionally refine by sampling a small sub-rectangle around the pixel mapped to that cell:
            // We'll average a small area inside the cell to decide whether cell is path (black) or not.
            int sx = Mathf.Clamp(Mathf.RoundToInt(cellX * cellWf), 0, imageWidth - 1);
            int sy = Mathf.Clamp(Mathf.RoundToInt(cellY * cellHf), 0, imageHeight - 1);
            int ex = Mathf.Clamp(Mathf.RoundToInt((cellX + 1) * cellWf) - 1, 0, imageWidth - 1);
            int ey = Mathf.Clamp(Mathf.RoundToInt((cellY + 1) * cellHf) - 1, 0, imageHeight - 1);

            // sample a smaller interior rectangle to avoid border noise (10% inset)
            int insetX = Mathf.Max(1, Mathf.RoundToInt((ex - sx + 1) * 0.1f));
            int insetY = Mathf.Max(1, Mathf.RoundToInt((ey - sy + 1) * 0.1f));
            int sampleX0 = Mathf.Clamp(sx + insetX, 0, imageWidth - 1);
            int sampleY0 = Mathf.Clamp(sy + insetY, 0, imageHeight - 1);
            int sampleX1 = Mathf.Clamp(ex - insetX, 0, imageWidth - 1);
            int sampleY1 = Mathf.Clamp(ey - insetY, 0, imageHeight - 1);

            // Clamp to safe area
            if (sampleX1 < sampleX0) sampleX1 = sampleX0;
            if (sampleY1 < sampleY0) sampleY1 = sampleY0;

            float sum = 0f;
            int count = 0;
            for (int yy = sampleY0; yy <= sampleY1; yy += Mathf.Max(1, (sampleY1 - sampleY0) / 6)) // sample grid (not every pixel)
            {
                for (int xx = sampleX0; xx <= sampleX1; xx += Mathf.Max(1, (sampleX1 - sampleX0) / 6))
                {
                    Color c = image.GetPixel(xx, yy);
                    sum += c.grayscale;
                    count++;
                }
            }
            float avg = count > 0 ? sum / count : 1f;

            // Decide whether this cell is path depending on invertColors and threshold. Use previously computed threshold as base.
            // If avg indicates path presence, we keep the cell as is (we already use cell coordinates).
            // But we won't change mapping: mapping to grid cell is strict; the avg can be used upstream to discard empty cells.

            // Return the cell coords (note: Grid Y axis in GridManager is bottom-to-top, image Y is top-to-bottom)
            // We must invert Y because image origin is usually top-left
            int gridX = cellX;
            int gridY = Mathf.Clamp(grid.height - 1 - cellY, 0, grid.height - 1);

            return new Vector2Int(gridX, gridY);
        }

        /// <summary>
        /// Creates an ArrowLine GameObject from a detected path (grid coordinates).
        /// Checks for occupied dots and overlaps with existing arrows.
        /// </summary>
        ArrowLine CreateArrowLineFromPath(List<Vector2Int> gridPath)
        {
            if (gridPath == null || gridPath.Count < 2) return null;

            // Check overlap with existing arrows in scene
            var existingArrows = FindObjectsByType<ArrowLine>(FindObjectsSortMode.None);
            var pathSet = new HashSet<Vector2Int>(gridPath);

            foreach (var existingArrow in existingArrows)
            {
                if (existingArrow.nodes == null) continue;

                var existingPathSet = new HashSet<Vector2Int>();
                foreach (var dot in existingArrow.nodes)
                {
                    if (dot != null)
                        existingPathSet.Add(dot.G);
                }

                float overlap = CalculateOverlapRatio(pathSet, existingPathSet);
                if (overlap > 0.5f) // More than 50% overlap
                {
                    Debug.Log($"Skipping arrow path - {overlap:P0} overlap with existing arrow: {string.Join(" -> ", gridPath)}");
                    return null;
                }
            }

            // Convert grid coordinates to Dot references
            var dotPath = new List<Dot>();
            int conflictCount = 0;

            foreach (var g in gridPath)
            {
                var dot = grid.GetDot(g);
                if (dot == null)
                {
                    Debug.LogWarning($"No Dot found at grid position {g}");
                    continue;
                }

                // Check if dot is already occupied
                if (dot.occupant != null)
                {
                    conflictCount++;
                }

                dotPath.Add(dot);
            }

            if (dotPath.Count < 2)
            {
                Debug.LogWarning($"Path too short after conversion: {dotPath.Count} dots");
                return null;
            }

            // If too many conflicts, skip this arrow
            float conflictRatio = conflictCount / (float)dotPath.Count;
            if (conflictRatio > 0.5f) // More than 50% of path is occupied
            {
                Debug.LogWarning($"Skipping arrow - {conflictRatio:P0} of path is occupied: {string.Join(" -> ", gridPath)}");
                return null;
            }

            if (conflictCount > 0)
            {
                Debug.LogWarning($"Arrow path has {conflictCount} occupied dots (out of {dotPath.Count}). Path: {string.Join(" -> ", gridPath)}");
            }

            // Create ArrowLine GameObject
            var go = Instantiate(arrowLinePrefab);
            var p = go.transform.position;
            p.z = -1f;
            go.transform.position = p;

            var al = go.GetComponent<ArrowLine>();
            if (al == null)
            {
                Destroy(go);
                return null;
            }

            al.nodes = dotPath;
            al.zOffset = -1f;
            al.occupyAllNodes = true;
            al.SyncVisualImmediate();

            // Claim head (even if occupied - screenshot import overrides)
            var head = al.CurrentHead;
            if (head)
                head.occupant = al;

            Debug.Log($"Created arrow with {dotPath.Count} nodes: {string.Join(" -> ", gridPath)}");
            return al;
        }

        /// <summary>
        /// Confirms screenshot arrows by moving them to the main createdArrows list.
        /// </summary>
        void ConfirmScreenshotArrows()
        {
            foreach (var arrow in screenshotArrows)
            {
                if (arrow != null && !createdArrows.Contains(arrow))
                {
                    createdArrows.Add(arrow);
                }
            }
            screenshotArrows.Clear();
            detectedArrowCount = 0;
            Debug.Log($"Confirmed {createdArrows.Count} total arrows (including screenshot import).");
        }

        /// <summary>
        /// Clears all arrows created from screenshot import.
        /// </summary>
        void ClearScreenshotArrows()
        {
            foreach (var arrow in screenshotArrows)
            {
                if (arrow != null)
                {
                    // Release occupied nodes
                    if (arrow.nodes != null)
                    {
                        foreach (var dot in arrow.nodes)
                        {
                            if (dot && dot.occupant == arrow)
                                dot.occupant = null;
                        }
                    }
                    Destroy(arrow.gameObject);
                }
            }
            screenshotArrows.Clear();
            detectedArrowCount = 0;
            Debug.Log("Cleared screenshot-imported arrows.");
        }
#endif
    }
}
