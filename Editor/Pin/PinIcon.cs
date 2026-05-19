#nullable enable

using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Connection / install state surfaced by the pin's status dot.
    /// </summary>
    public enum EPinStatus
    {
        CONNECTED,
        BUSY,
        NOT_RUNNING,
        BIND_FAILURE,
        NOT_INSTALLED,
    }

    /// <summary>
    /// Pin renderer — produces a composite <see cref="Texture2D"/> combining a placeholder
    /// background icon, a colored status dot in the bottom-right, and an optional blue
    /// update-available badge in the top-right. Used by <see cref="PinToolbarElement"/>
    /// to feed Unity's <see cref="UnityEditor.Toolbars.MainToolbarContent"/>.
    /// </summary>
    public static class PinIcon
    {
        #region CONSTANTS

        private const string ICON_RESOURCE_NAME = "pin-icon-placeholder";
        private const int CANVAS_SIZE = 20;
        private const int STATUS_DOT_SIZE = 6;
        private const int UPDATE_BADGE_SIZE = 4;

        private static readonly Color32 CONNECTED_COLOR = new(33, 196, 94, 255);
        private static readonly Color32 BUSY_COLOR = new(235, 179, 8, 255);
        private static readonly Color32 RED_COLOR = new(239, 68, 68, 255);
        private static readonly Color32 NOT_INSTALLED_COLOR = new(107, 115, 128, 255);
        private static readonly Color32 UPDATE_BADGE_COLOR = new(59, 130, 245, 255);

        #endregion

        #region FIELDS

        private static Texture2D? _backgroundIconCache;

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Loads the placeholder background icon on first call and caches the result.
        /// Returns <c>null</c> if the asset is not present (e.g. before the placeholder
        /// PNG has been added under <c>Editor/Resources/</c>).
        /// </summary>
        /// <returns>The cached background icon, or <c>null</c> when missing.</returns>
        private static Texture2D? LoadBackgroundIcon()
        {
            if (_backgroundIconCache == null)
            {
                _backgroundIconCache = UnityEngine.Resources.Load<Texture2D>(ICON_RESOURCE_NAME);
            }

            return _backgroundIconCache;
        }

        /// <summary>
        /// Maps an <see cref="EPinStatus"/> to the color used for the status dot.
        /// </summary>
        /// <param name="status">Pin status to translate.</param>
        /// <returns>Color drawn for the status dot. Falls back to gray for unknown values.</returns>
        private static Color32 GetStatusColor(EPinStatus status)
        {
            return status switch
            {
                EPinStatus.CONNECTED => CONNECTED_COLOR,
                EPinStatus.BUSY => BUSY_COLOR,
                EPinStatus.NOT_RUNNING => RED_COLOR,
                EPinStatus.BIND_FAILURE => RED_COLOR,
                EPinStatus.NOT_INSTALLED => NOT_INSTALLED_COLOR,
                _ => NOT_INSTALLED_COLOR,
            };
        }

        /// <summary>
        /// Copies the placeholder icon's pixels into <paramref name="canvas"/>, scaling
        /// nearest-neighbour to fit the canvas. If the icon is not present, fills the
        /// canvas with a low-alpha gray so the pin is still visible during development.
        /// </summary>
        /// <param name="canvas">Pixel buffer to write into. Length must be <c>CANVAS_SIZE * CANVAS_SIZE</c>.</param>
        private static void DrawBackground(Color32[] canvas)
        {
            var icon = LoadBackgroundIcon();

            if (icon == null)
            {
                var fallback = new Color32(140, 140, 140, 153);

                for (var i = 0; i < canvas.Length; i++)
                {
                    canvas[i] = fallback;
                }

                return;
            }

            var sourcePixels = icon.GetPixels32();
            var sourceWidth = icon.width;
            var sourceHeight = icon.height;

            for (var y = 0; y < CANVAS_SIZE; y++)
            {
                for (var x = 0; x < CANVAS_SIZE; x++)
                {
                    var sx = (int)((x / (float)CANVAS_SIZE) * sourceWidth);
                    var sy = (int)((y / (float)CANVAS_SIZE) * sourceHeight);
                    canvas[y * CANVAS_SIZE + x] = sourcePixels[sy * sourceWidth + sx];
                }
            }
        }

        /// <summary>
        /// Stamps a filled square of <paramref name="color"/> into <paramref name="canvas"/>
        /// at the given pixel coordinates. Pixels outside the canvas bounds are skipped.
        /// </summary>
        /// <param name="canvas">Pixel buffer to write into.</param>
        /// <param name="x">Left edge of the square (in canvas pixels).</param>
        /// <param name="y">Bottom edge of the square (in canvas pixels — Texture2D origin is bottom-left).</param>
        /// <param name="size">Side length of the square.</param>
        /// <param name="color">Fill color (replaces canvas alpha; opaque).</param>
        private static void StampSquare(Color32[] canvas, int x, int y, int size, Color32 color)
        {
            for (var dy = 0; dy < size; dy++)
            {
                var py = y + dy;

                if (py < 0 || py >= CANVAS_SIZE)
                {
                    continue;
                }

                for (var dx = 0; dx < size; dx++)
                {
                    var px = x + dx;

                    if (px < 0 || px >= CANVAS_SIZE)
                    {
                        continue;
                    }

                    canvas[py * CANVAS_SIZE + px] = color;
                }
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Builds a fresh composite <see cref="Texture2D"/> showing the placeholder icon
        /// plus the colored status dot and optional update badge. Caller takes ownership
        /// of the returned texture and is responsible for releasing it when no longer
        /// referenced (Unity's <see cref="UnityEditor.Toolbars.MainToolbar"/> retains
        /// the texture for the lifetime of the toolbar element).
        /// </summary>
        /// <param name="status">Current connection / install status; drives the dot color.</param>
        /// <param name="updateAvailable">When <c>true</c>, paints a blue update badge in the top-right corner.</param>
        /// <returns>A 20x20 RGBA <see cref="Texture2D"/> ready for use as a toolbar icon.</returns>
        public static Texture2D BuildComposite(EPinStatus status, bool updateAvailable)
        {
            var canvas = new Color32[CANVAS_SIZE * CANVAS_SIZE];

            DrawBackground(canvas);
            StampSquare(canvas, CANVAS_SIZE - STATUS_DOT_SIZE, 0, STATUS_DOT_SIZE, GetStatusColor(status));

            if (updateAvailable)
            {
                StampSquare(canvas, CANVAS_SIZE - UPDATE_BADGE_SIZE, CANVAS_SIZE - UPDATE_BADGE_SIZE, UPDATE_BADGE_SIZE, UPDATE_BADGE_COLOR);
            }

            var texture = new Texture2D(CANVAS_SIZE, CANVAS_SIZE, TextureFormat.RGBA32, mipChain: false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
            };
            texture.SetPixels32(canvas);
            texture.Apply();

            return texture;
        }

        #endregion
    }
}