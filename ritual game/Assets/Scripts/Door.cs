
using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    public Camera playerCamera;
    public float interactDistance = 3f;

    public GameObject confirmationPanel;
    public GameObject gameOverPanel;
    public TMPro.TMP_Text gameOverDayText;
    public GameObject dayText;

    private TaskRandomiser taskRandomizer;
    private bool popupOpen;
    private bool gameOver;

    void Start()
    {
        Time.timeScale = 1f;

        taskRandomizer = FindAnyObjectByType<TaskRandomiser>();

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
void Update()
{
    if (gameOver)
        return;

    if (taskRandomizer != null &&
        taskRandomizer.SequenceFailed)
    {
        EndGame();
        return;
    }

    if (popupOpen)
        return;

    // Left-click while looking at the door.
    if (Input.GetMouseButtonDown(0))
    {
        CheckDoorClick();
    }
}

    void CheckDoorClick()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            DoorInteraction door =
                hit.collider.GetComponentInParent<DoorInteraction>();

            if (door == this)
                OpenPopup();
        }
    }

    public void OpenPopup()
    {
        if (gameOver || popupOpen)
            return;

        popupOpen = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Asking whether all tasks are complete.");
    }

    // Connect this method to the Yes button.
    public void PressYes()
    {
        if (!popupOpen || gameOver)
            return;

        ClosePopup();

        if (taskRandomizer != null &&
            taskRandomizer.AllTasksCompleted())
        {
            Debug.Log("Day " + taskRandomizer.currentDay +
                      " completed successfully!");

            taskRandomizer.AdvanceDay();
        }
        else
        {
            EndGame();
        }
    }

    // Connect this method to the No button.
    public void PressNo()
    {
        if (!popupOpen || gameOver)
            return;

        ClosePopup();

        Debug.Log("Returning to today's tasks.");
    }

    void ClosePopup()
    {
        popupOpen = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

   void EndGame()
{
    gameOver = true;

    // Capture the day the player lost before resetting progress.
    int dayLost = taskRandomizer != null
        ? taskRandomizer.currentDay
        : 1;

    // Display the day the player lost.
    if (gameOverDayText != null)
    {
        gameOverDayText.text =
            "You Failed To Complete Your Morning Ritual On Day "
            + dayLost;
    }

    // Reset the saved day and ritual sequence.
    if (taskRandomizer != null)
    {
        taskRandomizer.ResetRitual();
    }
    else
    {
        PlayerPrefs.SetInt("CurrentDay", 1);
        PlayerPrefs.DeleteKey("MorningRitualSequence");
        PlayerPrefs.Save();
    }

    // Freeze gameplay.
    Time.timeScale = 0f;

    Debug.Log("GAME OVER! Lost on Day " + dayLost);

    // Show the Game Over UI.
    if (gameOverPanel != null)
        gameOverPanel.SetActive(true);

    // Hide the day counter.
    if (dayText != null)
        dayText.SetActive(false);

    // Unlock the cursor for UI interaction.
    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;
}

   
public void RestartGame()
{
    // Reset the entire ritual, including the saved task sequence.
    if (taskRandomizer == null)
        taskRandomizer = FindAnyObjectByType<TaskRandomiser>();

    if (taskRandomizer != null)
    {
        taskRandomizer.ResetRitual();
    }
    else
    {
        PlayerPrefs.SetInt("CurrentDay", 1);
        PlayerPrefs.DeleteKey("MorningRitualSequence");
        PlayerPrefs.Save();
    }

    // Restore normal gameplay time before reloading.
    Time.timeScale = 1f;

    // Reload the scene so Start() runs again.
    UnityEngine.SceneManagement.SceneManager.LoadScene(
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
    );
}

}
