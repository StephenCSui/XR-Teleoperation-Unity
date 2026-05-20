using UnityEngine;

public class ChangeColor : MonoBehaviour
{
    void Start()
    {
        // Change the object's material color to red when the game starts
        GetComponent<Renderer>().material.color = Color.red;
    }
}

