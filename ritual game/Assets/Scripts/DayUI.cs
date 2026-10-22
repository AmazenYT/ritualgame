
using UnityEngine;
using TMPro;

public class DayUI : MonoBehaviour
{
    public TMP_Text dayText;
    private TaskRandomiser taskRandomiser;

    void Start()
    {
        taskRandomiser = FindAnyObjectByType<TaskRandomiser>();

        if (taskRandomiser == null)
        {
            Debug.LogError("TaskRandomiser not found!");
            return;
        }

        UpdateDayText();
    }

    public void UpdateDayText()
    {
        if (dayText != null && taskRandomiser != null)
        {
            dayText.text = "Day " + taskRandomiser.currentDay;
        }
    }
}
