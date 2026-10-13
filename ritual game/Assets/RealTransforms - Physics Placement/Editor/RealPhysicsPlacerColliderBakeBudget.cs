using UnityEditor;
using UnityEngine;

namespace RealPhysicsPlacer.EditorTools
{
    internal static class RealPhysicsPlacerColliderBakeBudget
    {
        // Shared time budget for preview and bake operations. It keeps expensive
        // decomposition work cancelable and prevents long editor stalls.
        private const int DefaultBudgetSeconds = 30;
        private static double deadlineSeconds;

        internal static void Begin(RealPhysicsPlacerSettings settings)
        {
            deadlineSeconds = EditorApplication.timeSinceStartup + GetBudgetSeconds(settings);
        }

        internal static void End()
        {
            deadlineSeconds = 0d;
        }

        internal static bool IsExpired => deadlineSeconds > 0d && EditorApplication.timeSinceStartup >= deadlineSeconds;

        internal static int GetRemainingMilliseconds(RealPhysicsPlacerSettings settings)
        {
            var fallbackMilliseconds = GetBudgetSeconds(settings) * 1000;
            if (deadlineSeconds <= 0d)
            {
                return fallbackMilliseconds;
            }

            var remainingMilliseconds = Mathf.FloorToInt((float)((deadlineSeconds - EditorApplication.timeSinceStartup) * 1000d));
            return Mathf.Clamp(remainingMilliseconds, 0, fallbackMilliseconds);
        }

        private static int GetBudgetSeconds(RealPhysicsPlacerSettings settings)
        {
            return Mathf.Clamp(settings != null && settings.bakerBakeTimeBudgetSeconds > 0
                ? settings.bakerBakeTimeBudgetSeconds
                : DefaultBudgetSeconds, 5, 14400);
        }
    }
}
