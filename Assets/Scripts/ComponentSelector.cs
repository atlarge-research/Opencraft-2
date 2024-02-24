using UnityEngine;

public class ComponentSelector : MonoBehaviour
{
    public GameObject ECS_frontend;
    public GameObject Mirror_frontend;

    public enum DebugComponent
    {
        ECS,
        Mirror
    }

    public DebugComponent debugComponentSelection = DebugComponent.ECS; // Set default value here

    void Start()
    {
        if (!Application.isEditor)
        {
            // Check for command line arguments
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg == "-ecs")
                {
                    SetComponentSelection(DebugComponent.ECS);
                    return; // Exit the loop if a valid argument is found
                }
                else if (arg == "-mirror")
                {
                    SetComponentSelection(DebugComponent.Mirror);
                    return; // Exit the loop if a valid argument is found
                }
            }
        }

        // Default behavior if no command line argument is provided or if it's in the Unity Editor
        SetComponentSelection(debugComponentSelection);
    }

    void SetComponentSelection(DebugComponent selection)
    {
        switch (selection)
        {
            case DebugComponent.ECS:
                ECS_frontend.SetActive(true);
                Mirror_frontend.SetActive(false);
                break;
            case DebugComponent.Mirror:
                ECS_frontend.SetActive(false);
                Mirror_frontend.SetActive(true);
                break;
        }
    }
}
