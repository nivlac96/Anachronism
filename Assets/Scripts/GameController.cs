using UnityEngine;

/**
 * General purpose game controller attached to the GameController object in scene.
 * */
public class GameController : MonoBehaviour
{
    // singleton reference
    [HideInInspector] public static GameController instance;

    public void Awake()
    {
        // assign singelton reference
        instance = this;
    }

    public void OnRestartLevel()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
