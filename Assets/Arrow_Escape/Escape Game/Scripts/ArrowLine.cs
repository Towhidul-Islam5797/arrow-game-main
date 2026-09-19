using System.Collections.Generic;
using UnityEngine;
#if MT_DOTWEEN
using DG.Tweening;
#endif
using UnityEngine.Events;

namespace MultiTechStudio.EscapeGame
{
    public enum Dir { Up, Right, Down, Left, None }

    /// <summary>
    /// Controls a moving arrow line including tweened movement, collision, and exit behaviour.
    /// </summary>
    [RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
    public class ArrowLine : MonoBehaviour
    {
        [SerializeField] private ArrowFacingLines arrowFacingLines;
        [Header("Dots path (ordered)")]
        public List<Dot> nodes = new();

        [Header("Movement")]
        [Min(0.01f)] public float stepTime = 0.18f;
        public Color hitColor = Color.red;

        [Header("Rules")]
        public bool occupyAllNodes = true;   // keep true to forbid overlap with other arrows
        public float zOffset = -1f;          // render at z = -1

        private LineRenderer lr;
        private EdgeCollider2D edge;
#if MT_DOTWEEN
        private Tween moving;
#endif
        private int startIndex = 0; // head index in nodes

        [Header("Head Visual (optional)")]
        public Transform headVisual;          // assign the child "Head"
        public float headZ = -1f;
        [Header("Input (fallback)")]
        public bool manualClick = true;
        public Camera gameCamera; // assign if Camera.main is null


        Color baseColor;

        public bool debugLogs = true;

        private bool isMoving;
        private int queuedSteps = 0;
        public bool autoRunToEnd = true;

        // Exit mode
        [SerializeField] private float exitMargin = 1.5f;   // how far beyond screen to consider "gone"
        private bool isExiting = false;
        private Vector3 exitStep;                           // world delta per step while exiting


        // Expose for the saver
        public int StartIndexPublic => startIndex;

        [Header("Action")]
        public UnityEvent onLineExitedAction;
        public UnityEvent onLineHitAction;


        void Awake()
        {
            lr = GetComponent<LineRenderer>();
            edge = GetComponent<EdgeCollider2D>();
        }

        void Start()
        {
            if (nodes == null || nodes.Count == 0) return;

            baseColor = lr.material.HasProperty("_Color") ? lr.material.color : Color.white;
            var levelManager = LevelManager.Instance;
            if (levelManager != null)
            {
                onLineHitAction.AddListener(levelManager.OnLineHit);
            }

            ClaimAllNodes();
            UpdateHeadAtPathEnd();
        }

        Dot FindDotByGrid(Vector2Int g)
        {
            return GridManager.I ? GridManager.I.GetDotByGrid(g) : null;
        }

        void Update()
        {
            if (!manualClick) return;
            if (GameMode.I != null && !GameMode.IsPlay) return;
            var levelManager = LevelManager.Instance;
            if (levelManager != null && levelManager.levelState != LevelState.Playing) return;

            bool down = Input.GetMouseButtonDown(0)
                        || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            if (!down) return;

            var camUse = gameCamera != null ? gameCamera : Camera.main;
            if (!camUse) { if (debugLogs) Debug.LogWarning("[ArrowLine] No camera"); return; }

            Vector3 sp = (Input.touchCount > 0) ? (Vector3)Input.GetTouch(0).position : Input.mousePosition;
            Vector3 w = camUse.ScreenToWorldPoint(sp);
            Vector2 p2 = new Vector2(w.x, w.y);

            // use a radius based on visible line width
            float r = 0.5f;
            if (lr) r = Mathf.Max(lr.startWidth, lr.endWidth) * 0.6f; // tweak 0.5–0.8f to taste

            var hits = Physics2D.OverlapCircleAll(p2, r);
            if (debugLogs)
            {
                if (hits.Length == 0) Debug.Log($"[ArrowLine] OverlapCircle({p2}, r={r:F2}) — no hit");
                else foreach (var h in hits)
                        Debug.Log($"[ArrowLine] OverlapCircle hit {h.transform.name} (layer={LayerMask.LayerToName(h.gameObject.layer)})");
            }

            foreach (var h in hits)
            {
                if (h.transform == transform)
                {
                    if (debugLogs) Debug.Log("[ArrowLine] Clicked me → TryAdvance()");
                    TryAdvance();
                    SoundManager.Instance.PlaySound("arrow");
                    break;
                }
            }
        }



        void OnDestroy()
        {
#if MT_DOTWEEN
            transform.DOKill();
#endif
            // release any nodes we held (important if object is destroyed mid-play)
            if (nodes != null)
            {
                foreach (var d in nodes)
                    if (d && d.occupant == this) d.occupant = null;
            }
        }

        void UpdateHeadAtPathEnd()
        {
            if (!headVisual || nodes == null || nodes.Count < 2) return;

            // use grid coords for a clean cardinal direction
            var aG = nodes[^2].G;
            var bG = nodes[^1].G;
            var dir = DeltaToDir(bG - aG);
            float zRot = DirToZ(dir);

            // place & orient
            var p = nodes[^1].transform.position;
            p.z = headZ;
            headVisual.position = p;
            headVisual.rotation = Quaternion.Euler(0f, 0f, zRot);
            Vector3 _rot = headVisual.rotation.eulerAngles;
            _rot.z += 90f;
            headVisual.rotation = Quaternion.Euler(_rot);
        }

        /// <summary>
        /// Attempts to move the arrow forward, queueing additional steps if already tweening.
        /// </summary>
        public void TryAdvance()
        {
            if (isExiting) return;                 // already leaving, ignore input
            if (isMoving) { queuedSteps = Mathf.Min(queuedSteps + 1, 3); return; }
            StepOnce();
        }
        /// <summary>
        /// Begins the exit animation once the arrow reaches the end of the playable grid.
        /// </summary>
        void StartExit(Vector3 stepWorld)
        {
            if (isExiting) return;

            if (debugLogs) Debug.Log("[ArrowLine] Edge reached → exiting forward.");
            isExiting = true;
            ReleaseAllNodes();      // free any claimed nodes now that we’re leaving
            exitStep = stepWorld;   // keep moving by this world delta
            ExitStepOnce();
        }


        void UpdateHeadFromLR()
        {
            if (!headVisual) return;
            int n = lr.positionCount;
            if (n < 2) return;

            Vector3 a = lr.GetPosition(n - 2);
            Vector3 b = lr.GetPosition(n - 1);
            Vector3 d = (b - a).normalized;

            headVisual.position = new Vector3(b.x, b.y, headZ);

            // If your head sprite points up, subtract 90°.
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            headVisual.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        /// <summary>
        /// Advances the exit animation by one segment, sliding the rendered line forward.
        /// </summary>
        void ExitStepOnce()
        {
            int n = lr.positionCount;
            if (n == 0) return;

            // Snapshot current positions
            Vector3[] oldWorld = new Vector3[n];
            lr.GetPositions(oldWorld);

            // Build targets: shift body forward, push tip by exitStep
            Vector3[] newWorld = new Vector3[n];
            for (int i = 0; i < n - 1; i++) newWorld[i] = oldWorld[i + 1];
            newWorld[n - 1] = oldWorld[n - 1] + exitStep;

#if MT_DOTWEEN
            float t = 0f;
            moving = DOTween.To(() => t, v =>
            {
                t = v;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = Vector3.Lerp(oldWorld[i], newWorld[i], t);
                    lr.SetPosition(i, p);
                }
                SyncEdgeFromLR();
                UpdateHeadFromLR();   // keep head riding the tip
            }, 1f, stepTime).SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                for (int i = 0; i < n; i++) lr.SetPosition(i, newWorld[i]);
                SyncEdgeFromLR();
                UpdateHeadFromLR();

                if (IsCompletelyOffscreen())
                {
                    LevelManager.Instance?.OnLineExited(this);
                    if (debugLogs) Debug.Log("[ArrowLine] Off-screen → destroy (victory).");
                    Destroy(gameObject);
                }
                else
                {
                    ExitStepOnce(); // chain next exit step
                }
            });
#else
            // Fallback: instant update without tween
            for (int i = 0; i < n; i++) lr.SetPosition(i, newWorld[i]);
            SyncEdgeFromLR();
            UpdateHeadFromLR();

            if (IsCompletelyOffscreen())
            {
                LevelManager.Instance?.OnLineExited(this);
                if (debugLogs) Debug.Log("[ArrowLine] Off-screen → destroy (victory).");
                Destroy(gameObject);
            }
            else
            {
                Invoke(nameof(ExitStepOnce), stepTime);
            }
#endif
        }
        public void ApplySerializedPath(List<Dot> dots, int startIndexOverride = 0)
        {
            ReleaseAllNodes();

            nodes = dots ?? new List<Dot>();
            startIndex = Mathf.Clamp(startIndexOverride, 0, Mathf.Max(0, nodes.Count - 1));

            if (occupyAllNodes) ClaimAllNodes();
            else if (nodes.Count > 0) SafeClaim(nodes[startIndex]); // at least claim head

            SyncVisualImmediate();
        }


        bool IsCompletelyOffscreen()
        {
            Camera cam = gameCamera ? gameCamera : Camera.main;
            if (!cam) return true; // if no camera, just finish

            // Check all LR points are beyond the screen rect + margin
            Vector2 min = cam.ViewportToWorldPoint(new Vector3(0, 0, Mathf.Abs(zOffset - cam.transform.position.z)));
            Vector2 max = cam.ViewportToWorldPoint(new Vector3(1, 1, Mathf.Abs(zOffset - cam.transform.position.z)));

            // Expand bounds by exitMargin
            min -= Vector2.one * exitMargin;
            max += Vector2.one * exitMargin;

            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                var p = lr.GetPosition(i);
                if (p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y)
                    return false; // at least one point still in/near screen
            }
            return true;
        }


        private void StepOnce()
        {
            // Need at least two nodes to know the facing
            int n = nodes.Count;
            if (n < 2) return;

            var head = nodes[n - 1];
            var prev = nodes[n - 2];

            // facing dir = head - prev  (in grid space)
            Vector2Int dir = head.G - prev.G;
            Vector2Int nextG = head.G + dir;

            // find the target Dot in scene
            Dot nextDot = FindDotByGrid(nextG);
            if (nextDot == null)
            {
                // No grid dot ahead: start exit mode in the same direction the head faces.
                Vector3 stepWorld = nodes[n - 1].transform.position - nodes[n - 2].transform.position;
                onLineExitedAction?.Invoke();
                StartExit(stepWorld);
                return;
            }


            // occupancy check (ignore self)
            if (nextDot.occupant != null && nextDot.occupant != this)
            {
                if (debugLogs) Debug.Log($"[ArrowLine] Blocked by {nextDot.occupant.name} at {nextG}");
                // small flash
                onLineHitAction?.Invoke();
#if MT_DOTWEEN
                DOTween.Sequence()
                    .AppendCallback(() => SetLineColor(hitColor))
                    .AppendInterval(0.08f)
                    .AppendCallback(() => SetLineColor(baseColor));
#else
                // Fallback: simple flash without tween
                SetLineColor(hitColor);
                Invoke(nameof(ResetLineColor), 0.08f);
#endif
                queuedSteps = 0;
                return;
            }

            // Build tween arrays from current world points
            Vector3[] from = new Vector3[n];
            lr.GetPositions(from);

            // Update body list: drop tail, append new head
            Dot tail = nodes[0];
            if (occupyAllNodes && tail && tail.occupant == this) tail.occupant = null;

            for (int i = 0; i < n - 1; i++) nodes[i] = nodes[i + 1];
            nodes[n - 1] = nextDot;

            if (!occupyAllNodes) SafeClaim(nextDot);
            else SafeClaim(nextDot); // still mark head as ours

            // Targets after shift
            Vector3[] to = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var p = nodes[i].transform.position; p.z = zOffset;
                to[i] = p;
            }

            // Tween line
            isMoving = true;
#if MT_DOTWEEN
            float t = 0f;
            DOTween.To(() => t, v =>
            {
                t = v;
                for (int i = 0; i < n; i++)
                    lr.SetPosition(i, Vector3.Lerp(from[i], to[i], t));
                SyncEdgeFromLR();
                UpdateHeadFromLR();
            }, 1f, stepTime).SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                for (int i = 0; i < n; i++) lr.SetPosition(i, to[i]);
                SyncEdgeFromLR();
                UpdateHeadAtPathEnd();
                isMoving = false;

                // process queued taps
                if (queuedSteps > 0 || autoRunToEnd)
                {
                    queuedSteps--;
                    StepOnce();
                }
            });
#else
            // Fallback: instant update without tween
            for (int i = 0; i < n; i++) lr.SetPosition(i, to[i]);
            SyncEdgeFromLR();
            UpdateHeadAtPathEnd();
            isMoving = false;

            // process queued taps
            if (queuedSteps > 0 || autoRunToEnd)
            {
                queuedSteps--;
                Invoke(nameof(StepOnce), 0.01f);
            }
#endif
            if (LevelManager.Instance.currentLevelIndex != LevelManager.Instance.tutorialZoomLevel)
                LevelManager.Instance.HideTutorial();
        }
        /// <summary>
        /// Forces the line renderer and collider to match the current node list instantly.
        /// </summary>
        public void SyncVisualImmediate()
        {
            if (nodes == null || nodes.Count == 0) { if (debugLogs) Debug.LogWarning("[ArrowLine] SyncVisualImmediate: no nodes"); return; }
            var pts = GetSegmentWorlds(startIndex);
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            SyncEdgeFromLR();
            UpdateHeadFromLR();
            if (debugLogs) Debug.Log($"[ArrowLine] SyncVisualImmediate set {pts.Length} positions");
        }



        void SyncEdgeFromLR()
        {
            int n = lr.positionCount;
            if (n == 0) return;
            var pts2D = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var wp = lr.GetPosition(i);
                var lp = transform.InverseTransformPoint(wp);
                pts2D[i] = new Vector2(lp.x, lp.y);
            }
            edge.points = pts2D;
        }



        Vector3[] GetSegmentWorlds(int start)
        {
            int count = nodes.Count - start;
            if (count <= 0) count = 1;
            var arr = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var p = nodes[start + i].transform.position;
                p.z = zOffset;               // ← force z = -1 for every point
                arr[i] = p;
            }
            return arr;
        }

        public void SetBaseColor(Color c)
        {
            baseColor = c;
        }

        void SetLineColor(Color c)
        {
            if (lr.material.HasProperty("_Color")) lr.material.color = c;
            else lr.startColor = lr.endColor = c;
        }

        void ClaimAllNodes()
        {
            foreach (var d in nodes) SafeClaim(d);
        }
        void ReleaseAllNodes()
        {
            foreach (var d in nodes) if (d && d.occupant == this)
                {
                    d.occupant = null;
                }
        }
        void SafeClaim(Dot d)
        {
            if (!d) return;
            // if someone else is here, we still allow the component to exist,
            // but painting code should have prevented this already.
            if (d.occupant == null) d.occupant = this;
        }

        public bool HasExited => startIndex >= nodes.Count - 1;
        public Dot CurrentHead => nodes != null && nodes.Count > 0 ? nodes[startIndex] : null;

        static float DirToZ(Dir d) => d switch
        {
            Dir.Up => 0f,
            Dir.Right => -90f,
            Dir.Down => 180f,
            Dir.Left => 90f,
            _ => 0f
        };

        static Dir DeltaToDir(Vector2Int d)
        {
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
                return d.x > 0 ? Dir.Right : Dir.Left;
            else
                return d.y > 0 ? Dir.Up : Dir.Down;
        }

#if !MT_DOTWEEN
        private void ResetLineColor()
        {
            SetLineColor(baseColor);
        }
#endif

        public bool CanBeRemove()
        {
            if (nodes == null) return false;
            int n = nodes.Count;
            if (n < 2) return false;

            // head and previous to compute facing direction
            var head = nodes[n - 1];
            var prev = nodes[n - 2];

            Vector2Int dir = head.G - prev.G;
            if (dir == Vector2Int.zero) return false; // defensive

            // Probe forward from head: head.G + dir * step (step = 1..maxSteps)
            int maxSteps = Mathf.Max(n, 8); // check at least 'n' steps, or a small safety cap
            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2Int probeG = head.G + dir * step;
                Dot probeDot = FindDotByGrid(probeG);

                if (probeDot == null)
                {
                    // no dot ahead → the arrow can exit → removable
                    return true;
                }

                // If probeDot is occupied by another ArrowLine, it's blocked
                if (probeDot.occupant != null && probeDot.occupant != this)
                {
                    return false;
                }
            }
            return false;
        }
        public void BlinkArrowLine()
        {
#if MT_DOTWEEN
            DOTween.Sequence()
                .AppendCallback(() => SetLineColor(Color.white))
                .AppendInterval(0.5f)
                .AppendCallback(() => SetLineColor(Color.black)).SetLoops(-1, LoopType.Yoyo);
#else
            SetLineColor(Color.white);
#endif
        }

        public void ShowGrid()
        {
            if (arrowFacingLines == null) return;
            // compute head and prev to get facing direction and head world pos
            if (nodes != null && nodes.Count >= 2)
            {
                Dot head = nodes[nodes.Count - 1];
                Dot prev = nodes[nodes.Count - 2];

                Vector2Int dir = head.G - prev.G; // grid dir
                Vector3 headPos = head.transform.position;

                arrowFacingLines.ShowFacingLines(headPos, dir);
            }
        }
    }
}
