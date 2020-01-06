using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallMovement : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        if (!Camera.main.GetComponentInChildren<ServerToggle>().Server)
        {
            gameObject.transform.position = new Vector2(Camera.main.GetComponentInChildren<Client>().ballX, Camera.main.GetComponentInChildren<Client>().ballY);
        }
    }
}
