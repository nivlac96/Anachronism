using UnityEngine;

/**
 * General purpose game controller attached to the GameController object in scene.
 * */
public class GameController : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}
