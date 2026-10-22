
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TaskRandomiser : MonoBehaviour
{
    public int currentDay = 1;

    private const string DayKey = "CurrentDay";
    private const string SequenceKey = "MorningRitualSequence";

    private List<TaskObject.TaskType> dailyTasks =
        new List<TaskObject.TaskType>();

    // A List allows the same task type to be completed multiple times.
    private List<TaskObject.TaskType> completedTasks =
        new List<TaskObject.TaskType>();

    private TaskObject.TaskType[] availableTasks =
    {
        TaskObject.TaskType.Box,
        TaskObject.TaskType.Fridge,
        TaskObject.TaskType.Bowl
    };

    public bool SequenceFailed { get; private set; } = false;

    void Start()
    {
        currentDay = PlayerPrefs.GetInt(DayKey, 1);

        LoadSequence();

        // Day 1 starts with one random task.
        if (dailyTasks.Count == 0)
        {
            dailyTasks.Add(GetRandomTask());
            SaveSequence();
        }

        completedTasks.Clear();
        SequenceFailed = false;

        Debug.Log("Day " + currentDay + " has started!");
        Debug.Log("Number of required tasks: " + dailyTasks.Count);

        foreach (TaskObject.TaskType task in dailyTasks)
        {
            Debug.Log("Required task: " + task);
        }
    }

    private TaskObject.TaskType GetRandomTask()
    {
        int index = Random.Range(0, availableTasks.Length);
        return availableTasks[index];
    }

    private void LoadSequence()
    {
        dailyTasks.Clear();

        string savedSequence =
            PlayerPrefs.GetString(SequenceKey, "");

        if (string.IsNullOrEmpty(savedSequence))
            return;

        string[] entries = savedSequence.Split(',');

        foreach (string entry in entries)
        {
            if (int.TryParse(entry, out int taskValue) &&
                System.Enum.IsDefined(
                    typeof(TaskObject.TaskType), taskValue))
            {
                dailyTasks.Add(
                    (TaskObject.TaskType)taskValue);
            }
        }
    }

    private void SaveSequence()
    {
        List<string> entries = new List<string>();

        foreach (TaskObject.TaskType task in dailyTasks)
        {
            entries.Add(((int)task).ToString());
        }

        PlayerPrefs.SetString(
            SequenceKey, string.Join(",", entries));

        PlayerPrefs.Save();
    }

    public void CompleteTask(TaskObject taskObject)
    {
        if (taskObject == null || SequenceFailed)
            return;

        TaskObject.TaskType task = taskObject.taskType;

        // Count how many times this task is required today.
        int requiredCount = 0;

        foreach (TaskObject.TaskType requiredTask in dailyTasks)
        {
            if (requiredTask == task)
                requiredCount++;
        }

        // Count how many times this task has already been completed.
        int completedCount = 0;

        foreach (TaskObject.TaskType completedTask in completedTasks)
        {
            if (completedTask == task)
                completedCount++;
        }

        // Ignore tasks that are not required today.
        if (requiredCount == 0)
        {
            Debug.Log(task + " is not a required task today.");
            return;
        }

        // Don't allow more completions than are required.
        if (completedCount >= requiredCount)
        {
            Debug.Log("All " + task + " tasks have already been completed.");
            return;
        }

        completedTasks.Add(task);

        Debug.Log("Completed task: " + task);
        Debug.Log(
            "Progress: " + completedTasks.Count +
            "/" + dailyTasks.Count);

        if (AllTasksCompleted())
        {
            Debug.Log("Morning ritual complete! Return to the door.");
        }
    }

    public bool AllTasksCompleted()
    {
        return !SequenceFailed &&
               completedTasks.Count >= dailyTasks.Count;
    }

    public void AdvanceDay()
    {
        if (!AllTasksCompleted())
            return;

        currentDay++;

        // Preserve all previous tasks and add one new random task.
        dailyTasks.Add(GetRandomTask());

        PlayerPrefs.SetInt(DayKey, currentDay);
        SaveSequence();

        Debug.Log("Starting Day " + currentDay);
        Debug.Log("New ritual length: " + dailyTasks.Count);

        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex);
    }

    public void ResetRitual()
    {
        PlayerPrefs.SetInt(DayKey, 1);
        PlayerPrefs.DeleteKey(SequenceKey);
        PlayerPrefs.Save();
    }
}
