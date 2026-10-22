
using UnityEngine;
using TMPro;

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    public Camera playerCamera;
    public float interactDistance = 3f;

    [Header("Interaction UI")]
    public GameObject interactPrompt;
    public TMP_Text interactPromptText;
    public string promptMessage = "Press E to Interact";

    private TaskRandomiser taskRandomizer;
    private TaskObject currentTaskObject;

    void Start()
    {
        taskRandomizer = FindAnyObjectByType<TaskRandomiser>();

        if (taskRandomizer == null)
        {
            Debug.LogError("TaskRandomiser not found in the scene!");
        }

        // Hide the prompt when the game starts.
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }
    }

    void Update()
    {
        CheckForInteractable();

        if (currentTaskObject != null &&
            Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
    }

    void CheckForInteractable()
    {
        currentTaskObject = null;

        if (playerCamera == null)
        {
            HidePrompt();
            return;
        }

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            TaskObject taskObject =
                hit.collider.GetComponentInParent<TaskObject>();

            if (taskObject != null)
            {
                currentTaskObject = taskObject;
                ShowPrompt();
                return;
            }
        }

        HidePrompt();
    }

    void Interact()
    {
        if (currentTaskObject == null)
            return;

        if (taskRandomizer == null)
            return;

        taskRandomizer.CompleteTask(currentTaskObject);
    }

    void ShowPrompt()
    {
        if (interactPrompt != null &&
            !interactPrompt.activeSelf)
        {
            interactPrompt.SetActive(true);
        }

        if (interactPromptText != null)
        {
            interactPromptText.text = promptMessage;
        }
    }

    void HidePrompt()
    {
        if (interactPrompt != null &&
            interactPrompt.activeSelf)
        {
            interactPrompt.SetActive(false);
        }
    }
}
