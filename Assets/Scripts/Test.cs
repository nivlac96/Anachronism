using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public bool isMoving = true;
    public float speed = 2;

    
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<SpriteRenderer>().enabled = false; ;
    }

    // Update is called once per frame
    void Update()
    {
        if (isMoving)
        {
            gameObject.transform.position = new Vector2(gameObject.transform.position.x + speed * Time.deltaTime, gameObject.transform.position.y);
        }
    }
}
