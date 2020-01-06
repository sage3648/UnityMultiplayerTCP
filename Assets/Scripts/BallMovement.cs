using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class BallMovement : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        //Debug.Log(transform.position.y);
        if (!Camera.main.GetComponentInChildren<ServerToggle>().Server)
        {
            gameObject.transform.position = new Vector2(Camera.main.GetComponentInChildren<Client>().ballX, Camera.main.GetComponentInChildren<Client>().ballY);
        }else
        {

            if (Camera.main.GetComponentInChildren<Server>().clientConnected)
            {
                Camera.main.GetComponentInChildren<Server>().server.Send(Camera.main.GetComponentInChildren<Server>().client, Encoding.UTF8.GetBytes(gameObject.transform.position.y.ToString()));

            }
            //Debug.Log(gameObject.transform.position.x);
            //Debug.Log(gameObject.transform.position.y);
        }
    }
}
