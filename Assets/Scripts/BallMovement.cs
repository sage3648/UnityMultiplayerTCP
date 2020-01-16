using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class BallMovement : MonoBehaviour
{
    // Update is called once per frame
    void FixedUpdate()
    {
        //update location from server
        if (!Camera.main.GetComponentInChildren<ServerToggle>().Server)
        {
            gameObject.transform.position = new Vector2(Camera.main.GetComponentInChildren<Client>().ballX, Camera.main.GetComponentInChildren<Client>().ballY);
        }else
        {
            Debug.Log(transform.position.ToString("F6")); 
            if (Input.GetKeyDown(KeyCode.Space))
            {
                transform.GetComponentInChildren<Rigidbody2D>().AddForce(Vector2.up * 5, ForceMode2D.Impulse);
                Debug.Log("space pressed");
            }
            //send location data to client
            if (Camera.main.GetComponentInChildren<Server>().clientConnected)
            {
                Camera.main.GetComponentInChildren<Server>().server.Send(Camera.main.GetComponentInChildren<Server>().client, Encoding.UTF8.GetBytes(gameObject.transform.position.y.ToString()));
            }
        }
    }
}
