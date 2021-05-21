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

    // Update is called once per frame
    void Update()
    {
        // Reset the level when 'R' is pressed
        if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
