using UnityEngine;

namespace Gamesim.Presentation
{
    public static class PhoneDisplay
    {
        public static bool Active => UnityEngine.Device.Application.isMobilePlatform;
        private static int width, height;
        private static float pixelsPerUnit = 1f, nextMeasurement;

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern float GamesimWindowLongEdgePoints();
#endif

        public static float PixelsPerUnit(int renderWidth, int renderHeight)
        {
            if (width == renderWidth && height == renderHeight && Time.unscaledTime < nextMeasurement)
                return pixelsPerUnit;
            width = renderWidth;
            height = renderHeight;
            float logicalEdge = 0f;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var resources = activity.Call<AndroidJavaObject>("getResources"))
            using (var metrics = resources.Call<AndroidJavaObject>("getDisplayMetrics"))
            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
            using (var view = window.Call<AndroidJavaObject>("getDecorView"))
            {
                float density = metrics.Get<float>("density");
                if (density > 0f)
                    logicalEdge = Mathf.Max(view.Call<int>("getWidth"), view.Call<int>("getHeight")) / density;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            logicalEdge = GamesimWindowLongEdgePoints();
#endif
            // A Simulator profile or a not-yet-sized native window can use its reported DPI.
            pixelsPerUnit = logicalEdge > 0f ? Mathf.Max(width, height) / logicalEdge
                : Mathf.Max(1f, UnityEngine.Device.Screen.dpi / 160f);
            pixelsPerUnit = Mathf.Max(.25f, pixelsPerUnit);
            nextMeasurement = Time.unscaledTime + (logicalEdge > 0 ? 5f : .5f);
            return pixelsPerUnit;
        }
    }
}
