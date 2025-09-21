using System;
using System.Net;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RemotePlayControl
{
    private const int Port = 54021;
    private static TcpServer server;

    static RemotePlayControl()
    {
        CreateAndStartServer();
        EditorApplication.quitting += DisposeServer;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
    }

    private static void CreateAndStartServer()
    {
        DisposeServer();
        server = new TcpServer(IPAddress.Loopback, Port, CommandFactory.Create());
        server.Start();
    }

    private static void OnBeforeAssemblyReload()
    {
        DisposeServer();
    }

    private static void OnAfterAssemblyReload()
    {
        CreateAndStartServer();
    }

    public static void DisposeServer()
    {
        try
        {
            server?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RemotePlayControl] Error disposing server: {ex}");
        }
        finally
        {
            server = null;
        }
    }
}