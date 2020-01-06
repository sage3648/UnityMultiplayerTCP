using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleTcp;
using System;
using System.Threading.Tasks;
using System.Text;
using UnityEngine.UI;

public class Client : MonoBehaviour
{
    public float ballX, ballY; 
    private void Start()
    {
        if (!Camera.main.GetComponentInChildren<ServerToggle>().Server)
        {
            TcpClient client = new TcpClient("192.168.1.100", 9000, false, null, null);

            client.Connected = Connected;
            client.Disconnected = Disconnected;
            client.DataReceived = DataReceived;

            try
            {
                client.Connect();
            }
            catch (NullReferenceException)
            {
                Debug.Log("Unable to connect to server");
            }
            client.Send(Encoding.UTF8.GetBytes("Player Connected"));

        }
    }

    private async Task Disconnected()
    {
        Debug.Log("Disconnected from server");
    }

    private async Task DataReceived(byte[] arg)
    {
        Debug.Log("Data received: " + Encoding.UTF8.GetString(arg));
        ballX = float.Parse(Encoding.UTF8.GetString(arg));
        ballY = float.Parse(Encoding.UTF8.GetString(arg));
     
    }

    private async Task Connected()
    {
        Debug.Log("Connected to server");
    }
}
