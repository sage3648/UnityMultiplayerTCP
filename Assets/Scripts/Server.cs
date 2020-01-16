using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleTcp;
using System;
using System.Threading.Tasks;
using System.Text;
using UnityEngine.UI;

public class Server : MonoBehaviour
{
    public GameObject Ball, Floor;
    public PhysicsMaterial2D Material;
    private bool isServer;
    private Vector2 ballLocation;
    public String client; 
    public bool clientConnected; 
    public TcpServer server = new TcpServer("0.0.0.0", 9000, false, null, null);

    void Start()
    {
     
        isServer = Camera.main.GetComponentInChildren<ServerToggle>().Server;
        if (isServer)
        {
            clientConnected = false; 
            server.ClientConnected = ClientConnected;
            server.DataReceived = DataReceived;
            server.ClientDisconnected = ClientDisconnected;
            server.Start();
            Debug.Log("Server Started");
            Console.WriteLine("Server Started");
            Ball.AddComponent<CircleCollider2D>();
            Ball.AddComponent<Rigidbody2D>();
            Ball.GetComponentInChildren<Rigidbody2D>().sharedMaterial = Material;
        }
    }

    private Task ClientDisconnected(string arg1, DisconnectReason arg2)
    {
        Debug.Log("Client Disconnected");

        throw new NotImplementedException();
    }

    private Task DataReceived(string arg1, byte[] arg2)
    {
        throw new NotImplementedException();
    }

    private async Task ClientConnected(string arg)
    {
        clientConnected = true;
        client = arg;
    }

    private void LateUpdate()
    {

        if (isServer && clientConnected == true)
        {
            //ballLocation = Ball.transform.position;
            //for(int i = 0; i < clients.Count; i++)
            //{
               
            //}
        }
    }
}







