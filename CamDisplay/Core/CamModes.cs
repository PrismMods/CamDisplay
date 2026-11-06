using System;
using System.Collections.Generic;

namespace CamDisplay
{
    /* The resolution + frame-rate choices offered for a camera, as {width, height, fps}.

       Built from what the camera reports (one {width, height, minFps, maxFps} per format and frame
       rate range), because cameras differ widely: a FaceTime HD camera offers seven sizes at 15-30
       fps, a capture card dozens. Each range offers its top rate plus the common lower ones it
       covers, since a lower rate is the cheapest way to save CPU. Where the camera cannot be asked
       (no lookup on this OS), the short fixed list stands in. */
    internal static class CamModes
    {
        internal static readonly int[][] Fallback =
        {
            new[] { 640, 360, 30 }, new[] { 1280, 720, 30 }, new[] { 1920, 1080, 30 }, new[] { 1280, 720, 60 },
        };

        private static readonly int[] Common = { 60, 30, 15 };

        internal static List<int[]> From(IEnumerable<double[]> ranges)
        {
            var modes = new List<int[]>();
            foreach (var r in ranges)
            {
                int w = (int)r[0], h = (int)r[1];
                if (w <= 0 || h <= 0 || r[3] <= 0) continue;
                Add(modes, w, h, (int)Math.Round(r[3]));
                foreach (int fps in Common)
                    if (fps < r[3] && fps >= r[2] - 0.01) Add(modes, w, h, fps);
            }
            // Smallest picture first, then by frame rate: the order the fixed list always had.
            modes.Sort((a, b) =>
            {
                int c = ((long)a[0] * a[1]).CompareTo((long)b[0] * b[1]);
                if (c == 0) c = a[0].CompareTo(b[0]);
                return c != 0 ? c : a[2].CompareTo(b[2]);
            });
            return modes;
        }

        internal static int IndexOf(IList<int[]> modes, int width, int height, int fps)
        {
            for (int i = 0; i < modes.Count; i++)
                if (modes[i][0] == width && modes[i][1] == height && modes[i][2] == fps) return i;
            return -1;
        }

        internal static string Name(int[] m) => m[0] + "×" + m[1] + " @ " + m[2];

        private static void Add(List<int[]> modes, int w, int h, int fps)
        {
            if (fps > 0 && IndexOf(modes, w, h, fps) < 0) modes.Add(new[] { w, h, fps });
        }
    }
}
