
using UnityEngine;
using UnityEngine.SceneManagement;

public class TaskRandomiser : MonoBehaviour
{
    public int currentDay = 1;

    public bool boxIsCorrect;
    public bool fridgeIsCorrect;
    public bool bowlIsCorrect;

    private bool boxCompleted;
    private bool fridgeCompleted;
    private bool bowlCompleted;

    void Start()
    {
        currentDay = PlayerPrefs.GetInt("CurrentDay", 1);
        StartNewDay();
    }

    public void StartNewDay()
    {
        boxIsCorrect = false;
        fridgeIsCorrect = false;
        bowlIsCorrect = false;

        boxCompleted = false;
        fridgeCompleted = false;
        bowlCompleted = false;

        int dailyTasks = Random.Range(0, 7);

        switch (dailyTasks)
        {
            case 0:
                boxIsCorrect = true;
                Debug.Log("Today's task: Cereal Box");
                break;

            case 1:
                fridgeIsCorrect = true;
                Debug.Log("Today's task: Fridge");
                break;

            case 2:
                boxIsCorrect = true;
                fridgeIsCorrect = true;
                Debug.Log("Today's tasks: Cereal Box and Fridge");
                break;
            case 3:
                bowlIsCorrect = true;
                Debug.Log("Today's task: Bowl");
                break;
            case 4:
                boxIsCorrect = true;
                bowlIsCorrect = true;
                Debug.Log("Today's tasks: Cereal Box and Bowl");
                break;
            case 5:
                fridgeIsCorrect = true;
                bowlIsCorrect = true;
                Debug.Log("Today's tasks: Fridge and Bowl");
                break;
            case 6:
                boxIsCorrect = true;
                fridgeIsCorrect = true;
                bowlIsCorrect = true;
                Debug.Log("Today's tasks: Cereal Box, Fridge, and Bowl");
                break;
        }

        Debug.Log("Day " + currentDay + " has started!");
    }

    public bool IsCorrect(TaskObject taskObject)
    {
        if (taskObject.taskType == TaskObject.TaskType.Box)
            return boxIsCorrect;

        if (taskObject.taskType == TaskObject.TaskType.Fridge)
            return fridgeIsCorrect;
        if (taskObject.taskType == TaskObject.TaskType.Bowl)
            return bowlIsCorrect;

        return false;
    }

    public void CompleteTask(TaskObject taskObject)
    {
        if (!IsCorrect(taskObject))
        {
            Debug.Log("WRONG TASK: " + taskObject.taskType);
            return;
        }

        if (taskObject.taskType == TaskObject.TaskType.Box)
        {
            if (boxCompleted)
            {
                Debug.Log("Cereal Box task already completed today.");
                return;
            }

            boxCompleted = true;
        }
        else if (taskObject.taskType == TaskObject.TaskType.Fridge)
        {
            if (fridgeCompleted)
            {
                Debug.Log("Fridge task already completed today.");
                return;
            }

            fridgeCompleted = true;
        }
        else if (taskObject.taskType == TaskObject.TaskType.Bowl)
        {
            if (bowlCompleted)
            {
                Debug.Log("Bowl task already completed today.");
                return;
            }

            bowlCompleted = true;
        }
        Debug.Log("Correct task completed: " + taskObject.taskType);

        
    }

    public bool AllTasksCompleted()
    {
        bool boxDone = !boxIsCorrect || boxCompleted;
        bool fridgeDone = !fridgeIsCorrect || fridgeCompleted;
        bool bowlDone = !bowlIsCorrect || bowlCompleted;

        return boxDone && fridgeDone && bowlDone;
    }

    public void AdvanceDay()
{
    currentDay++;

    // Save the day number before reloading.
    PlayerPrefs.SetInt("CurrentDay", currentDay);
    PlayerPrefs.Save();

    Debug.Log("Starting Day " + currentDay);

    // Ensure gameplay is unpaused.
    Time.timeScale = 1f;

    // Reload the current scene.
    SceneManager.LoadScene(
        SceneManager.GetActiveScene().buildIndex
    );
}
}
