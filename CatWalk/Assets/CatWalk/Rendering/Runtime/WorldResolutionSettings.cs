using UnityEngine;

namespace CatWalk.Rendering
{
    public enum WorldUpscaling { Integer, Fit }

    [CreateAssetMenu(menuName = "CatWalk/Rendering/Tuning/World Resolution", fileName = "WorldResolution")]
    public sealed class WorldResolutionSettings : ScriptableObject
    {
        public bool Enabled = true;
        [Tooltip("Настоящее разрешение рендера 3D-мира, включая небо, частицы и дизеринг. UI использует разрешение экрана. Диапазон: 16–4096.")]
        [Min(16)] public int Width = 640;
        [Min(16)] public int Height = 360;
        [Tooltip("Integer: одинаковые квадратные пиксели и целый множитель. Fit: заполнить доступную площадь с сохранением пропорций. Оба режима используют nearest-neighbor.")]
        public WorldUpscaling Upscaling = WorldUpscaling.Integer;
        public Color BorderColor = Color.black;

        public Vector2Int Resolution => new Vector2Int(Mathf.Clamp(Width, 16, 4096), Mathf.Clamp(Height, 16, 4096));

        // Pixel coordinates, origin at bottom left, matching Camera.WorldToViewportPoint.
        public Rect OutputRect(int width, int height)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            Vector2Int size = Resolution;
            float scale = Mathf.Min(width / (float)size.x, height / (float)size.y);
            // A window smaller than the authored image must downsample to show the whole frame.
            if (Upscaling == WorldUpscaling.Integer && scale >= 1) scale = Mathf.Floor(scale);
            int w = Mathf.Min(width, Mathf.Max(1, Mathf.RoundToInt(size.x * scale)));
            int h = Mathf.Min(height, Mathf.Max(1, Mathf.RoundToInt(size.y * scale)));
            return new Rect((width - w) / 2, (height - h) / 2, w, h);
        }
    }
}
