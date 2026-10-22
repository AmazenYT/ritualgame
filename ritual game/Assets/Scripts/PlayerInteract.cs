
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public Camera playerCamera;
    public float interactDistance = 3f;

    private TaskRandomiser taskRandomizer;

    void Start()
    {
        taskRandomizer = FindAnyObjectByType<TaskRandomiser>();

        if (taskRandomizer == null)
        {
            Debug.LogError("TaskRandomiser not found in the scene!");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
    }

    void Interact()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            TaskObject taskObject =
                hit.collider.GetComponentInParent<TaskObject>();

            if (taskObject == null)
                return;

            if (taskRandomizer == null)
                return;

            taskRandomizer.CompleteTask(taskObject);
        }
    }
}
