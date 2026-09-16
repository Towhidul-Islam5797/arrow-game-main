using UnityEngine;


namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Simple runtime toggle between draw and play modes for testing.
    /// </summary>
    public class GameMode : MonoBehaviour
    {
        public static GameMode I { get; private set; }

        public enum Mode { Draw, Play }
        public Mode mode = Mode.Draw;

        [Tooltip("Show a tiny HUD in top-left with mode + hint")]
        public bool showHud = true;

        void Awake() { I = this; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
                mode = (mode == Mode.Draw) ? Mode.Play : Mode.Draw;

            // Optional: step all arrows in Play with Space
            if (mode == Mode.Play && Input.GetKeyDown(KeyCode.Space))
            {
                foreach (var l in FindObjectsByType<ArrowLine>(FindObjectsSortMode.None))
                    if (l) l.TryAdvance();
            }
        }

        public static bool IsDraw => I && I.mode == Mode.Draw;
        public static bool IsPlay => I && I.mode == Mode.Play;

#if UNITY_EDITOR
        void OnGUI()
        {
            if (!showHud) return;
            const float pad = 10f;
            const float boxHeight = 48f;
            float yPos = Screen.height - boxHeight - pad;
            var rect = new Rect(pad, yPos, 420, boxHeight);
            GUI.Box(rect, "");
            GUI.Label(new Rect(pad + 6, yPos + 6, 4000, 20),
                $"Mode: {(mode == Mode.Draw ? "DRAW ✏️" : "PLAY ▶")}  |  Press H to toggle  |  (Space = step all in Play)");
        }
#endif
    }
}
