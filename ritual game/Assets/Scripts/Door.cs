
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
        if (gameOver || popupOpen)
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
    int dayLost = taskRandomizer != null
        ? taskRandomizer.currentDay
        : 1;

    // Display the lost day on the Game Over UI.
    if (gameOverDayText != null)
    {
        gameOverDayText.text = "You Failed To Complete Your Morning Ritual On Day " + dayLost;
    }

        PlayerPrefs.SetInt("CurrentDay", 1);
        PlayerPrefs.Save();

        Time.timeScale = 0f;

        Debug.Log("GAME OVER! You did not complete all today's tasks.");

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        if (dayText != null)
            dayText.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
{
    // Reset the saved day.
    PlayerPrefs.SetInt("CurrentDay", 1);
    PlayerPrefs.Save();

    // Unfreeze the game.
    Time.timeScale = 1f;

    // Reload the current scene.
        UnityEngine.SceneManagement.SceneManager.LoadScene(
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
    );
}
}
