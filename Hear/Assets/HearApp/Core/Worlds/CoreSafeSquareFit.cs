using UnityEngine;

namespace HearApp.Core.Worlds
{
    /// <summary>
    /// Implements the "Core Safe Square" responsive composition rule (docs/05) for a
    /// single camera, shared by all three worlds regardless of 2D/2.5D/3D technology:
    /// a roughly square, centered composition always stays fully framed, and any extra viewport
    /// space (landscape width, portrait height) is used to reveal more of the scene rather than
    /// cropping the core or stretching art non-uniformly.
    ///
    /// Works for both orthographic cameras (2D/2.5D worlds) and perspective cameras (3D world) by
    /// holding the dimension that matches the safe square fixed, and letting the camera reveal
    /// more along whichever dimension the current viewport has extra room in.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CoreSafeSquareFit : MonoBehaviour
    {
        [Tooltip("Orthographic half-size (world units) of the core safe square, used when the camera is orthographic.")]
        [SerializeField] private float orthographicCoreHalfSize = 5f;

        [Tooltip("Vertical field of view (degrees) representing the core safe square at aspect 1:1, used when the camera is perspective.")]
        [SerializeField] private float perspectiveCoreVerticalFovDeg = 50f;

        private Camera _camera;
        private int _lastWidth = -1;
        private int _lastHeight = -1;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            Apply();
        }

        private void LateUpdate()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
                Apply();
        }

        private void Apply()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            float aspect = _lastHeight > 0 ? (float)_lastWidth / _lastHeight : 1f;

            if (_camera.orthographic)
            {
                // aspect >= 1 (landscape/square): keep vertical half-size fixed, width reveals more.
                // aspect < 1 (portrait): keep horizontal half-size fixed instead, height reveals more.
                _camera.orthographicSize = aspect >= 1f
                    ? orthographicCoreHalfSize
                    : orthographicCoreHalfSize / aspect;
            }
            else
            {
                float baseHalfAngle = perspectiveCoreVerticalFovDeg * Mathf.Deg2Rad * 0.5f;
                _camera.fieldOfView = aspect >= 1f
                    ? perspectiveCoreVerticalFovDeg
                    : Mathf.Clamp(2f * Mathf.Atan(Mathf.Tan(baseHalfAngle) / aspect) * Mathf.Rad2Deg, 1f, 179f);
            }
        }

        /// <summary>
        /// Normalized viewport rect (0..1) of the centered safe square for the given aspect ratio.
        /// Useful for worlds that want to keep specific UI/gameplay elements guaranteed on-screen.
        /// </summary>
        public static Rect ComputeSafeSquareViewportRect(float aspect)
        {
            if (aspect >= 1f)
            {
                float w = 1f / aspect;
                return new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else
            {
                float h = aspect;
                return new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
        }
    }
}
