using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


namespace MultiTechStudio.EscapeGame
{
    public enum LevelState
    {
        NotStarted,
        Playing,
        Completed,
        Failed,
        Paused
    }
    /// <summary>
    /// Coordinates level loading, progression, and arrow line lifecycle management.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        public UiManager uiManager;
        public FirebaseManager firebaseManager;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private TutorialManager tutorialManager;

        [Header("Scene Refs")]
        public Transform arrowsParent;                // where new arrows are parented
        public Transform dotsParent;                  // optional: where Dot components live (else whole scene)
        public ArrowLine arrowLinePrefab;

        [Header("Theme (Optional)")]
        [Tooltip("If assigned, will use arrow prefab from CurrentTheme at runtime. Leave empty to use arrowLinePrefab directly.")]
        [SerializeField] private CurrentTheme currentThemeAsset;

        [Header("Level Assets")]
        public List<LevelAsset> levels = new List<LevelAsset>();
        public bool autoLoadOnPlay = true;
        public bool autoRunToEnd = true;

        [Header("Events")]
        public UnityEvent onLevelWin;

        public int currentLevelIndex
        {
            get => PlayerPrefs.GetInt("LevelIndex", 0);
            private set => PlayerPrefs.SetInt("LevelIndex", value);
        }
        public int winCoinAmount = 10;
        public int coinsRequiredToRetry = 50;
        public int startLives = 3;
        [SerializeField] private float moveSpeedForStep = 0.1f;
        public int tutorialZoomLevel = 5;
        public LevelState levelState = LevelState.NotStarted;

        // fast lookup: grid G -> Dot
        private Dictionary<Vector2Int, Dot> _dotIndex;

        // at the top of LevelManager, near your other fields
        private readonly List<ArrowLine> _spawnedLines = new();  // track spawned lines so ClearAllArrows() can clean them


        void Awake()
        {
            Instance = this;
            if (!arrowsParent)
            {
                var t = transform.Find("_Arrows");
                arrowsParent = t ? t : new GameObject("_Arrows").transform;
                arrowsParent.SetParent(transform, false);
            }
        }

        void Start()
        {
            if (GameMode.I && GameMode.IsPlay)
                levelState = LevelState.Playing;

            // Apply theme arrow prefab if CurrentTheme is assigned
            ApplyThemeArrowPrefab();

            if (autoLoadOnPlay && levels != null && levels.Count > 0)
            {
                var idx = Mathf.Clamp(currentLevelIndex, 0, levels.Count - 1);
                LoadLevel(idx);
            }
            if (uiManager != null)
                onLevelWin.AddListener(uiManager.LevelComplete);
        }

        /// <summary>
        /// Applies the arrow prefab from CurrentTheme if assigned. This allows runtime theme switching.
        /// </summary>
        private void ApplyThemeArrowPrefab()
        {
            if (currentThemeAsset != null && currentThemeAsset.ActiveTheme != null)
            {
                if (currentThemeAsset.ActiveTheme.arrowPrefab != null)
                {
                    ArrowLine themeArrowLine = currentThemeAsset.ActiveTheme.arrowPrefab.GetComponent<ArrowLine>();
                    if (themeArrowLine != null)
                    {
                        arrowLinePrefab = themeArrowLine;
                    }
                }
            }
        }

        public void LoadLevel(int index)
        {
            ClearAllArrows();
            if (levels == null || levels.Count == 0)
            {
                Debug.LogWarning("[LevelManager] No LevelAsset assigned.");
                return;
            }
            if (index < 0 || index >= levels.Count)
            {
                index = 0;
                currentLevelIndex = index;
                Debug.LogWarning($"[LevelManager] Level index {index} out of range.");
            }

            currentLevelIndex = index;
            var level = levels[index];
            if (!level) { Debug.LogWarning("[LevelManager] LevelAsset is null."); return; }

            if (!GridManager.I)
            {
                Debug.LogError("[LevelManager] GridManager.I is null — place a GridManager in the scene.");
                return;
            }
            GridManager.I.ApplyFromAsset(level);
            if (cameraController != null)
                cameraController.SetForNewLevel(level.camSize);
            BuildDotIndex();

            if (tutorialManager != null)
                tutorialManager.ShowTutorial(currentLevelIndex);

            if (level.arrows != null)
                foreach (var def in level.arrows) TrySpawnLineFromDef(def);
            winCoinAmount = level.coinsEarned;
            Invoke(nameof(ClearGrid), 0.5f);
        }
        private void ClearGrid()
        {
            GridManager.I.DeleteNotUsedDots();
        }
        private ArrowLine TrySpawnLineFromDef(ArrowLineDef def)
        {
            if (def == null) return null;
            if (!arrowLinePrefab)
            {
                Debug.LogError("[LevelManager] ArrowLine prefab not assigned.");
                return null;
            }
            if (!GridManager.I)
            {
                Debug.LogError("[LevelManager] GridManager.I is null.");
                return null;
            }

            var nodeDots = new List<Dot>(def.path?.Count ?? 0);
            if (def.path == null || def.path.Count < 2)
            {
                Debug.LogWarning("[LevelManager] ArrowLineDef has <2 points; skipped.");
                return null;
            }
            foreach (var g in def.path)
            {
                var d = GridManager.I.GetDot(g);
                if (!d)
                {
                    Debug.LogWarning($"[LevelManager] Missing Dot at {g}; skipping this ArrowLine.");
                    return null;
                }
                nodeDots.Add(d);
            }

            var firstPos = nodeDots[0].transform.position;
            var go = Instantiate(arrowLinePrefab, firstPos, Quaternion.identity, arrowsParent);
            var line = go.GetComponent<ArrowLine>();
            if (!line) { Debug.LogError("[LevelManager] Spawned prefab has no ArrowLine."); Destroy(go); return null; }

            // line.stepTime = def.stepTime;
            line.stepTime = moveSpeedForStep;
            line.zOffset = def.zOffset;
            line.occupyAllNodes = def.occupyAllNodes;
            line.headZ = def.headZ;
            line.hitColor = def.hitColor;
            line.autoRunToEnd = autoRunToEnd;
            var lr = line.GetComponent<LineRenderer>();
            if (lr)
            {
                if (lr.material && lr.material.HasProperty("_Color")) lr.material.color = def.lineColor;
                else { lr.startColor = def.lineColor; lr.endColor = def.lineColor; }
            }

            line.nodes = nodeDots;

            var fi = typeof(ArrowLine).GetField("startIndex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null) fi.SetValue(line, Mathf.Clamp(def.startIndex, 0, nodeDots.Count - 1));

            line.SyncVisualImmediate();

            _spawnedLines.Add(line);
            return line;
        }

        public void FindLineCanBeRemove()
        {
            foreach (ArrowLine line in _spawnedLines)
            {
                if (line.CanBeRemove())
                {
                    line.BlinkArrowLine();
                    return;
                }
            }
        }
        public void ShowGrid()
        {
            if (_spawnedLines.Count <= 0) return;
            int _total = 0;
            int _max = Mathf.Clamp(Random.Range(4, 15), 1, _spawnedLines.Count);
            foreach (ArrowLine line in _spawnedLines)
            {
                _total++;
                if (_total >= _max) return;
                line.ShowGrid();
            }
        }


        public void ReloadLevel()
        {
            GridManager.I.ClearGridOccupant();
            if (currentLevelIndex >= 0) LoadLevel(currentLevelIndex);
        }

        public void IncreaseLevelNumber()
        {
            if (levels == null || levels.Count == 0) return;
            int next = currentLevelIndex + 1;
            if (next >= levels.Count) next = 0;
            currentLevelIndex = next;
        }
        public void LoadNextLevel()
        {
            LoadLevel(currentLevelIndex);
        }

        public void HideTutorial()
        {
            if (tutorialManager != null)
                tutorialManager.HideTutorial();
        }
        public void ShowTutorial()
        {
            if (tutorialManager != null)
                tutorialManager.ShowTutorial(currentLevelIndex);
        }
        // ---------- Dot Index ----------
        private void BuildDotIndex()
        {
            _dotIndex = new Dictionary<Vector2Int, Dot>(256);
            Dot[] dots;
            if (dotsParent)
                dots = dotsParent.GetComponentsInChildren<Dot>(true);
            else
                dots = FindObjectsByType<Dot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // whole scene fallback

            foreach (var d in dots)
            {
                // last one wins if duplicates exist
                _dotIndex[d.G] = d;
            }
        }


        public void ClearAllArrows()
        {
            for (int i = _spawnedLines.Count - 1; i >= 0; i--)
            {
                var l = _spawnedLines[i];
                if (!l) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(l.gameObject);
                else Destroy(l.gameObject);
#else
                Destroy(l.gameObject);
#endif
            }
            _spawnedLines.Clear();
        }


        // Optional: call this from ArrowLine when it destroys itself on exit
        public void OnLineExited(ArrowLine line)
        {
            if (_spawnedLines.Contains(line))
            {
                _spawnedLines.Remove(line);
                if (_spawnedLines.Count == 0) onLevelWin?.Invoke();
            }
            SoundManager.Instance.PlaySound("whoosh");
        }

        // ---------- Utils ----------
        public void OnLineHit()
        {
            if (uiManager != null)
                uiManager.RemoveLife();

            SoundManager.Instance.PlaySound("block");
        }
    }
}
