using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace RealPhysicsPlacer.EditorTools
{
    internal static class RealPhysicsPlacerTimingLog
    {
        internal static bool Enabled { get; set; }

        internal static Stopwatch Start()
        {
            return Stopwatch.StartNew();
        }

        internal static void Step(string label, Stopwatch stopwatch)
        {
            if (!Enabled || stopwatch == null)
            {
                return;
            }

            Debug.Log($"[RealTransforms Timing] {label}: {stopwatch.Elapsed.TotalMilliseconds:0.0} ms");
            stopwatch.Restart();
        }

        internal static void Total(string label, Stopwatch stopwatch)
        {
            if (!Enabled || stopwatch == null)
            {
                return;
            }

            Debug.Log($"[RealTransforms Timing] {label}: {stopwatch.Elapsed.TotalMilliseconds:0.0} ms total");
        }
    }
}
