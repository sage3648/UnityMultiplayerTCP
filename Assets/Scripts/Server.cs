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
    private List<String> clients; 
    TcpServer server = new TcpServer("0.0.0.0", 9000, false, null, null);

    void Start()
    {
        isServer = Camera.main.GetComponentInChildren<ServerToggle>().Server;
        if (isServer)
        {
            server.ClientConnected = ClientConnected;
            server.DataReceived = DataReceived;
            server.ClientDisconnected = ClientDisconnected;
            server.Start();
            Console.WriteLine("Server Started");
            Ball.AddComponent<CircleCollider2D>();
            Ball.AddComponent<Rigidbody2D>();
            Ball.GetComponentInChildren<Rigidbody2D>().sharedMaterial = Material;
        }
    }

    private Task ClientDisconnected(string arg1, DisconnectReason arg2)
    {
        throw new NotImplementedException();
    }

    private Task DataReceived(string arg1, byte[] arg2)
    {
        throw new NotImplementedException();
    }

    private async Task ClientConnected(string arg)
    {
        clients.Add(arg);
    }

    private void LateUpdate()
    {
        if (isServer)
        {
            ballLocation = Ball.transform.position;
            for(int i = 0; i < clients.Count; i++)
            {
                server.Send(clients[i], Encoding.UTF8.GetBytes(ballLocation.x.ToString()));
                server.Send(clients[i], Encoding.UTF8.GetBytes(ballLocation.y.ToString()));
            }
        }
    }
}







