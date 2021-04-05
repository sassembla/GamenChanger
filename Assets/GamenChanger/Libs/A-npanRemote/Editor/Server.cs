using UnityEngine;
using System;
using UnityEditor;
using System.IO;
using WebuSocketCore.Server;

[InitializeOnLoad]
public class Server
{
    private const int PORT_NUMBER = 11874;// good story

    private enum ServerState
    {
        None,
        Running
    };
    private static ServerState serverState = ServerState.None;

    static Server()
    {
        var first = true;
        Action serverStop = null;

        EditorApplication.update += () =>
        {
            var a = Application.isPlaying;
            var b = EditorApplication.isPlaying;
            var c = EditorApplication.isPlayingOrWillChangePlaymode;
            if (a && b && c)
            {
                if (first)
                {
                    Debug.Log("start server.");
                    first = false;
                    serverState = ServerState.Running;
                    serverStop = StartServer();
                }
            }

            if (a && b && !c)
            {
                serverState = ServerState.None;
                serverStop?.Invoke();
            }

            if (serverState == ServerState.Running)
            {
                return;
            }
        };
    }

    private static Action StartServer()
    {
        ClientConnection localSocket = null;
        ClientConnection remoteSocket = null;
        var server = new WebuSocketServer(
            PORT_NUMBER,
            newConnection =>
            {
                if (newConnection.RequestHeaderDict.ContainsKey("local"))
                {
                    localSocket = newConnection;
                    newConnection.OnMessage = segments =>
                    {
                        while (0 < segments.Count)
                        {
                            var data = segments.Dequeue();
                            var bytes = new byte[data.Count];
                            Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);

                            remoteSocket?.Send(bytes);
                        }
                    };
                }
                else
                {
                    remoteSocket = newConnection;
                    newConnection.OnMessage = segments =>
                    {
                        while (0 < segments.Count)
                        {
                            var data = segments.Dequeue();
                            var bytes = new byte[data.Count];
                            Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);

                            localSocket?.Send(bytes);
                        }
                    };
                }
            }
        );

        Action serverStop = () =>
        {
            server?.Dispose();
        };
        return serverStop;
    }
}