
using UnityEngine;

public class TaskRandomiser : MonoBehaviour
{
    public bool boxIsCorrect;
    public bool fridgeIsCorrect;

    void Start()
    {
         Debug.Log("TaskRandomizer started!");
        // Reset both tasks
        boxIsCorrect = false;
        fridgeIsCorrect = false;

        // Choose one of three possible daily task sets
        int dailyTasks = Random.Range(0, 3);

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
        }
    }

    public bool IsCorrect(TaskObject taskObject)
    {
        if (taskObject.taskType == TaskObject.TaskType.Box)
            return boxIsCorrect;

        if (taskObject.taskType == TaskObject.TaskType.Fridge)
            return fridgeIsCorrect;

        return false;
    }
}
