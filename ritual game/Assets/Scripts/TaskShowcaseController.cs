
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaskShowcaseController : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera showcaseCamera;
    [SerializeField] private Camera gameplayCamera;

    [Header("Task System")]
    [SerializeField] private TaskRandomiser taskRandomiser;
    [SerializeField] private TaskShowcaseTarget[] taskTargets;

    [Header("Highlight Effect")]
    [SerializeField] private GameObject highlightEffectPrefab;
    [SerializeField] private Vector3 highlightOffset = Vector3.zero;

    [Header("Timing")]
    [SerializeField, Min(0.1f)]
    private float highlightDuration = 1.5f;

    [SerializeField, Min(0f)]
    private float endingDelay = 0.5f;

    [SerializeField, Min(0f)]
    private float gapBetweenHighlights = 0.3f;

    [Header("Player Controls")]
    [SerializeField] private Behaviour[] movementScripts;
    [SerializeField] private Behaviour[] interactionScripts;

    private bool[] movementWasEnabled;
    private bool[] interactionWasEnabled;

    private GameObject highlightEffectInstance;
    private ParticleSystem[] highlightParticleSystems;

    private void Start()
    {
        StartCoroutine(BeginShowcase());
    }

    private IEnumerator BeginShowcase()
    {
        // Allow TaskRandomiser.Start() to load the saved sequence.
        yield return null;

        if (taskRandomiser == null)
        {
            taskRandomiser =
                FindAnyObjectByType<TaskRandomiser>();
        }

        if (taskRandomiser == null)
        {
            Debug.LogError(
                "SHOWCASE: TaskRandomiser not found.");
            yield break;
        }

        if (showcaseCamera == null ||
            gameplayCamera == null)
        {
            Debug.LogError(
                "SHOWCASE: Assign both cameras in the Inspector.");
            yield break;
        }

        if (highlightEffectPrefab == null)
        {
            Debug.LogError(
                "SHOWCASE: Assign the highlight effect prefab.");
            yield break;
        }

        // Save and disable player controls during the showcase.
        SaveAndDisableScripts(
            movementScripts,
            ref movementWasEnabled);

        SaveAndDisableScripts(
            interactionScripts,
            ref interactionWasEnabled);

        // Switch to the showcase camera.
        showcaseCamera.enabled = true;
        gameplayCamera.enabled = false;

        List<TaskObject.TaskType> tasks =
            taskRandomiser.GetDailyTasks();

        if (tasks == null || tasks.Count == 0)
        {
            Debug.LogWarning(
                "SHOWCASE: No daily tasks were returned.");

            FinishShowcase();
            yield break;
        }

        Debug.Log(
            "SHOWCASE: Starting memory showcase. Highlights: "
            + tasks.Count);

        // Display each required task in sequence.
        foreach (TaskObject.TaskType task in tasks)
        {
            TaskShowcaseTarget target = FindTarget(task);

            if (target == null)
            {
                Debug.LogWarning(
                    "SHOWCASE: No target assigned for task: "
                    + task);
            }
            else
            {
                Vector3 position =
                    target.transform.position + highlightOffset;

                PlayHighlightAt(position);

                Debug.Log(
                    "SHOWCASE: Highlighting task: " + task);
            }

            yield return new WaitForSecondsRealtime(
                highlightDuration);

            StopHighlight();

            if (gapBetweenHighlights > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    gapBetweenHighlights);
            }
        }

        yield return new WaitForSecondsRealtime(
            endingDelay);

        FinishShowcase();
    }

    private TaskShowcaseTarget FindTarget(
        TaskObject.TaskType task)
    {
        if (taskTargets == null)
            return null;

        foreach (TaskShowcaseTarget target in taskTargets)
        {
            if (target != null &&
                target.taskType == task)
            {
                return target;
            }
        }

        return null;
    }

    private void PlayHighlightAt(Vector3 position)
    {
        // Ensure any previous effect is removed.
        StopHighlight();

        if (highlightEffectPrefab == null)
        {
            Debug.LogError(
                "SHOWCASE: Highlight effect prefab is missing.");
            return;
        }

        // Instantiate a fresh effect for every highlight.
        highlightEffectInstance = Instantiate(
            highlightEffectPrefab,
            position,
            Quaternion.identity
        );

        highlightParticleSystems =
            highlightEffectInstance
                .GetComponentsInChildren<ParticleSystem>(true);

        Debug.Log(
            $"SHOWCASE: Instantiated effect at {position}. " +
            $"Particle systems found: " +
            $"{highlightParticleSystems.Length}");

        if (highlightParticleSystems.Length == 0)
        {
            Debug.LogError(
                "SHOWCASE: No Particle Systems found on " +
                "the prefab or its children.");
            return;
        }

        // Make sure the effect is active before playback.
        highlightEffectInstance.SetActive(true);

        foreach (ParticleSystem ps in highlightParticleSystems)
        {
            if (ps == null)
                continue;

            ps.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            ps.Clear(true);
            ps.Play(true);

            Debug.Log(
                $"SHOWCASE: Playing '{ps.name}', " +
                $"isPlaying = {ps.isPlaying}");
        }
    }

    private void StopHighlight()
    {
        if (highlightEffectInstance == null)
            return;

        // Destroy the complete effect, including child objects.
        Destroy(highlightEffectInstance);

        highlightEffectInstance = null;
        highlightParticleSystems = null;
    }

    private void FinishShowcase()
    {
        StopHighlight();

        // Restore the normal gameplay camera.
        if (showcaseCamera != null)
            showcaseCamera.enabled = false;

        if (gameplayCamera != null)
            gameplayCamera.enabled = true;

        // Restore each script to its previous enabled state.
        RestoreScripts(
            movementScripts,
            movementWasEnabled);

        RestoreScripts(
            interactionScripts,
            interactionWasEnabled);

        Debug.Log(
            "SHOWCASE: Complete. Player control restored.");
    }

    private void SaveAndDisableScripts(
        Behaviour[] scripts,
        ref bool[] previousStates)
    {
        if (scripts == null)
            return;

        previousStates = new bool[scripts.Length];

        for (int i = 0; i < scripts.Length; i++)
        {
            if (scripts[i] == null)
                continue;

            previousStates[i] = scripts[i].enabled;
            scripts[i].enabled = false;
        }
    }

    private void RestoreScripts(
        Behaviour[] scripts,
        bool[] previousStates)
    {
        if (scripts == null || previousStates == null)
            return;

        for (int i = 0; i < scripts.Length; i++)
        {
            if (scripts[i] != null)
            {
                scripts[i].enabled = previousStates[i];
            }
        }
    }
}
