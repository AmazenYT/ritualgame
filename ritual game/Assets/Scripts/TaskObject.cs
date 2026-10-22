
using UnityEngine;

public class TaskObject : MonoBehaviour
{
    public enum TaskType
    {
        Box,
        Fridge,
        Bowl,
        Sink,
        Bag,
        Garden
    }

    public TaskType taskType;
}
