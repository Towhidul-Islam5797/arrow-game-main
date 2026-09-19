using UnityEngine;

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Provides pinch-and-pan controls for the orthographic gameplay camera.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private GameObject background;
        [Header("Zoom Settings")]
        [SerializeField] private float minZoom = 3f;
        public float maxZoom = 12f;
        [SerializeField] private float zoomSpeed = 0.5f;
        [SerializeField] private float scrollWheelSensitivity = 2f;

        [Header("Pan Settings")]
        [SerializeField] private bool enablePanLimits = true;
        [SerializeField] private Vector2 panLimitMin = new Vector2(-10f, -10f);
        [SerializeField] private Vector2 panLimitMax = new Vector2(10f, 10f);

        private Camera cam;
        private float targetZoom;
        private Vector3 lastTouchPosition;
        private bool isDragging = false;
        private float initialOrthographicSize;
        private Vector3 initialBackgroundScale;
        private Vector3 initialPosition;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (!cam)
            {
                Debug.LogError("CameraController requires a Camera component!");
                enabled = false;
                return;
            }

            initialOrthographicSize = cam.orthographicSize;
            initialPosition = transform.position;

            // Store initial background scale if background is assigned
            if (background != null)
            {
                initialBackgroundScale = background.transform.localScale;
            }
        }

        void Update()
        {
            if (LevelManager.Instance.currentLevelIndex < 5) return;
            if (LevelManager.Instance.levelState == LevelState.Playing)
            {
                HandlePinchZoom();
                HandleMouseScrollZoom();
                HandlePanInput();
                ApplySmoothZoom();
            }
        }

        void HandlePinchZoom()
        {
            // Check for two touches (pinch gesture)
            if (Input.touchCount == 2)
            {
                isDragging = false; // Disable panning during pinch

                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                // Get previous touch positions
                Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

                // Calculate distances
                float prevTouchDeltaMag = (touch0PrevPos - touch1PrevPos).magnitude;
                float touchDeltaMag = (touch0.position - touch1.position).magnitude;

                // Calculate zoom delta
                float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                // Apply zoom
                targetZoom += deltaMagnitudeDiff * zoomSpeed * Time.deltaTime;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

                if (LevelManager.Instance.currentLevelIndex == LevelManager.Instance.tutorialZoomLevel)
                    LevelManager.Instance.HideTutorial();
            }
        }

        void HandleMouseScrollZoom()
        {
            // Mouse scroll wheel for desktop testing
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                targetZoom -= scroll * scrollWheelSensitivity;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
                if (LevelManager.Instance.currentLevelIndex == LevelManager.Instance.tutorialZoomLevel)
                    LevelManager.Instance.HideTutorial();
            }
        }

        void HandlePanInput()
        {
            // Handle touch input
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    isDragging = true;
                    lastTouchPosition = GetWorldPosition(touch.position);
                }
                else if (touch.phase == TouchPhase.Moved && isDragging && touch.deltaPosition.magnitude > 10f)
                {
                    Vector3 currentTouchPosition = GetWorldPosition(touch.position);
                    Vector3 delta = lastTouchPosition - currentTouchPosition;

                    Vector3 newPosition = transform.position + delta;

                    // Apply pan limits if enabled
                    if (enablePanLimits)
                    {
                        newPosition.x = Mathf.Clamp(newPosition.x, panLimitMin.x, panLimitMax.x);
                        newPosition.y = Mathf.Clamp(newPosition.y, panLimitMin.y, panLimitMax.y);
                    }

                    transform.position = newPosition;
                    lastTouchPosition = GetWorldPosition(touch.position);
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    isDragging = false;
                }
            }

            // Handle mouse input for desktop testing
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                lastTouchPosition = GetWorldPosition(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                Vector3 currentMousePosition = GetWorldPosition(Input.mousePosition);
                Vector3 delta = lastTouchPosition - currentMousePosition;

                Vector3 newPosition = transform.position + delta;

                // Apply pan limits if enabled
                if (enablePanLimits)
                {
                    newPosition.x = Mathf.Clamp(newPosition.x, panLimitMin.x, panLimitMax.x);
                    newPosition.y = Mathf.Clamp(newPosition.y, panLimitMin.y, panLimitMax.y);
                }

                transform.position = newPosition;
                lastTouchPosition = GetWorldPosition(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }
        }

        void ApplySmoothZoom()
        {
            // Smoothly interpolate to target zoom
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * 10f);

            // Scale background to maintain same visual size regardless of zoom
            if (background != null && initialOrthographicSize > 0)
            {
                float scaleFactor = cam.orthographicSize / initialOrthographicSize;
                background.transform.localScale = initialBackgroundScale * scaleFactor;
            }
        }

        Vector3 GetWorldPosition(Vector3 screenPosition)
        {
            Vector3 worldPos = cam.ScreenToWorldPoint(screenPosition);
            worldPos.z = transform.position.z; // Keep the camera's Z position
            return worldPos;
        }

        public void SetForNewLevel(float zoom)
        {
            transform.position = initialPosition;
            SetZoom(zoom);
        }

        // Public methods to adjust zoom programmatically
        public void SetZoom(float zoom)
        {
            maxZoom = zoom + 2;
            targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }

        public void ResetCamera(Vector3 position, float zoom)
        {
            transform.position = position;
            targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            cam.orthographicSize = targetZoom;
        }
    }
}

