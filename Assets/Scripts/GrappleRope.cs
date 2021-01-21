using UnityEngine;

/**
 * Class for a gameobject that creates a string of points resembling a rope in between a start and end point
 */
public class GrappleRope : MonoBehaviour
{
    public int numberOfPoints = 8;
    public GameObject pointPrefab; 
    
    private Vector2 startPoint;
    private Vector2 anchorPoint;
    private bool isEngaged = false;

    private GameObject[] pointsArray;

    // Start is called before the first frame update
    void Start()
    {
        pointsArray = new GameObject[numberOfPoints];
        for (int i = 0; i < numberOfPoints; i++)
        {
            pointsArray[i] = Instantiate(pointPrefab, transform);
            pointsArray[i].gameObject.SetActive(false);
        }
    }

    public void StartDrawingRope(Vector2 _anchorPoint)
    {
        isEngaged = true;
        anchorPoint = _anchorPoint;

        for (int i = 0; i < numberOfPoints; i++)
        {
            pointsArray[i].gameObject.SetActive(true);
        }
    }

    public void HideRope()
    {
        isEngaged = false;
        for (int i = 0; i < numberOfPoints; i++)
        {
            pointsArray[i].gameObject.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < numberOfPoints; i++)
        {
            pointsArray[i].transform.position = Vector2.Lerp(transform.position, anchorPoint, (i * 1.0f / numberOfPoints));
        }
    }
}
