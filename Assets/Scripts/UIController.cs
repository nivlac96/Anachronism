using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    // singleton reference
    [HideInInspector] public static UIController instance;
    
    // game object reference set in inspector
    public Text versionNumber;

    void Awake()
    {
        // assign singleton reference
        instance = this;
        
        // set version number
        if (versionNumber)
        {
            versionNumber.text = "Version " + Application.version;
        }
        
    }
}
