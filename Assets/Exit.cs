using UnityEngine;

public class ExitGame : MonoBehaviour
{
    public void ExitApplication()
    {
        // This only works in a built application, not in the editor
        Application.Quit();

        // Optional: Print message to console so you know it's working in the Editor
        Debug.Log("Exit button pressed. Application would quit if this were a build.");
    }
}
