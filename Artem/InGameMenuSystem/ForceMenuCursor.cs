using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[DefaultExecutionOrder(10000)] // run very late
public class ForceMenuCursor : MonoBehaviour
{
    private void OnEnable()
    {
        // In case you enter the scene directly in Play mode
        StartCoroutine(EnsureCursorAfterLoad());
        // Also handle when coming from another scene
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene s, LoadSceneMode m) => StartCoroutine(EnsureCursorAfterLoad());

    private IEnumerator EnsureCursorAfterLoad()
    {
        // Let everyone else (controllers, focus handlers) run first
        yield return null;                  // next frame
        yield return new WaitForEndOfFrame(); // very end of that frame

        // For menus, Confined is more stable in-editor than None
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    // Belt & suspenders: keep it correct while menu is open
    private void LateUpdate()
    {
        if (!Application.isFocused) return; // don’t fight focus changes
        if (Cursor.lockState != CursorLockMode.Confined)
            Cursor.lockState = CursorLockMode.Confined;
        if (!Cursor.visible)
            Cursor.visible = true;
    }
}
