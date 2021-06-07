using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;

[RequireComponent(typeof(Text))]
public class GetButtonNames : MonoBehaviour
{
    void Awake()
    {
        Text textField = GetComponent<Text>();
        string rawString = textField.text;

        var regex = new Regex("{(.*?)}");
        var matches = regex.Matches(rawString);
        foreach (Match match in matches) //you can loop through your matches like this
        {
            var valueWithoutBrackets = match.Groups[1].Value; // name, name@gmail.com


            // var valueWithBrackets = match.Value; // {name}, {name@gmail.com}

            //PlayerInput pi = GetComponent<PlayerInput>();
            //Debug.Log(pi.currentActionMap["Jump"].GetBindingDisplayString(InputBinding.DisplayStringOptions.DontOmitDevice));
        }
    }
}
