
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
            taskRandomiser = FindAnyObjectByType<TaskRandomiser>();

        if (taskRandomiser == null)
        {
            Debug.LogError(
                "TaskShowcaseController: TaskRandomiser not found.");
            yield break;
        }

        if (showcaseCamera == null || gameplayCamera == null)
        {
            Debug.LogError(
                "TaskShowcaseController: Assign both cameras.");
            yield break;
        }

        if (highlightEffectPrefab == null)
        {
            Debug.LogError(
                "TaskShowcaseController: Assign the highlight effect prefab.");
            yield break;
        }

        SaveAndDisableScripts(
            movementScripts, ref movementWasEnabled);

        SaveAndDisableScripts(
            interactionScripts, ref interactionWasEnabled);

        showcaseCamera.enabled = true;
        gameplayCamera.enabled = false;

        List<TaskObject.TaskType> tasks =
            taskRandomiser.GetDailyTasks();

        Debug.Log(
            "Starting memory showcase. Highlights: " + tasks.Count);

        foreach (TaskObject.TaskType task in tasks)
        {
            TaskShowcaseTarget target = FindTarget(task);

            if (target == null)
            {
                Debug.LogWarning(
                    "No showcase target assigned for task: " + task);
            }
            else
            {
                PlayHighlightAt(target.transform.position + highlightOffset);

                Debug.Log("Showcase highlight: " + task);
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

        yield return new WaitForSecondsRealtime(endingDelay);

        FinishShowcase();
    }

    private TaskShowcaseTarget FindTarget(
        TaskObject.TaskType task)
    {
        if (taskTargets == null)
            return null;

        foreach (TaskShowcaseTarget target in taskTargets)
        {
            if (target != null && target.taskType == task)
                return target;
        }

        return null;
    }

    
private void PlayHighlightAt(Vector3 position)
{
    if (highlightEffectPrefab == null)
    {
        Debug.LogError("Highlight effect prefab is missing.");
        return;
    }

    // Create the reusable effect the first time it is needed.
    if (highlightEffectInstance == null)
    {
        highlightEffectInstance = Instantiate(
            highlightEffectPrefab,
            position,
            Quaternion.identity
        );

        highlightParticleSystems =
            highlightEffectInstance
                .GetComponentsInChildren<ParticleSystem>(true);
    }

    // Stop and clear every particle system before moving it.
    foreach (ParticleSystem ps in highlightParticleSystems)
    {
        if (ps != null)
        {
            ps.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
            ps.Clear(true);
        }
    }

    // Move and activate the reusable effect.
    highlightEffectInstance.transform.SetPositionAndRotation(
        position,
        Quaternion.identity
    );

    highlightEffectInstance.SetActive(true);

    // Restart all particle systems, including child systems.
    foreach (ParticleSystem ps in highlightParticleSystems)
    {
        if (ps != null)
            ps.Play(true);
    }

    Debug.Log(
        "Playing highlight at position: " + position +
        ". Particle systems found: " +
        (highlightParticleSystems != null
            ? highlightParticleSystems.Length
            : 0)
    );

    if (highlightParticleSystems == null ||
        highlightParticleSystems.Length == 0)
    {
        Debug.LogError(
            "The assigned prefab contains no Particle Systems."
        );
    }
}

private void StopHighlight()
{
    if (highlightEffectInstance == null)
        return;

    foreach (ParticleSystem ps in highlightParticleSystems)
    {
        if (ps != null)
        {
            ps.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    highlightEffectInstance.SetActive(false);
}


    private void FinishShowcase()
    {
        StopHighlight();

        showcaseCamera.enabled = false;
        gameplayCamera.enabled = true;

        RestoreScripts(movementScripts, movementWasEnabled);
        RestoreScripts(interactionScripts, interactionWasEnabled);

        Debug.Log(
            "Memory showcase complete. Player control restored.");
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
                scripts[i].enabled = previousStates[i];
        }
    }
}
