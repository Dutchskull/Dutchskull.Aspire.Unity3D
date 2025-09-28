using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

internal class TcpServer : IDisposable
{
    private readonly IPAddress ip;
    private readonly int port;
    private readonly ICommandDispatcher dispatcher;
    private Thread listenerThread;
    private TcpListener listener;
    private volatile bool running;
    private bool disposed;

    public TcpServer(IPAddress ip, int port, ICommandDispatcher dispatcher)
    {
        this.ip = ip ?? throw new ArgumentNullException(nameof(ip));
        this.port = port;
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public void Start()
    {
        if (running || disposed)
        {
            return;
        }

        running = true;
        listenerThread = new Thread(ListenerLoop) { IsBackground = true };
        listenerThread.Start();
        Debug.Log($"[RemotePlayControl] Listening on {ip}:{port}");
    }

    public void Stop()
    {
        if (!running)
        {
            return;
        }

        running = false;
        try { listener?.Stop(); } catch { }
        try { listenerThread?.Join(500); } catch { }
    }

    private void ListenerLoop()
    {
        try
        {
            listener = new TcpListener(ip, port);
            listener.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[RemotePlayControl] Failed to start listener: {e}");
            running = false;
            throw e;
        }

        while (running)
        {
            try
            {
                if (!listener.Pending())
                {
                    Thread.Sleep(100);
                    continue;
                }

                using TcpClient client = listener.AcceptTcpClient();
                using NetworkStream stream = client.GetStream();
                client.ReceiveTimeout = 2000;
                client.SendTimeout = 2000;

                ListenerLoopBody(stream);
            }
            catch (SocketException ex)
            {
                Debug.LogError($"[RemotePlayControl] Socket exception: {ex}");
                throw ex;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayControl] Listener exception: {ex}");
                throw ex;
            }
        }

        try { listener?.Stop(); } catch { }
    }

    private void ListenerLoopBody(NetworkStream stream)
    {
        string request = ReadStringFromStream(stream);
        if (string.IsNullOrEmpty(request))
        {
            WriteStringToStream(stream, "error:empty_request");
            return;
        }

        (string cmd, string arg) = ParseHttpCommand(request);
        string responseBody = DispatchOnMainThread(cmd, arg);

        string http = "HTTP/1.1 200 OK\r\n" +
                      "Content-Type: text/plain; charset=utf-8\r\n" +
                      $"Content-Length: {Encoding.UTF8.GetByteCount(responseBody)}\r\n" +
                      "Connection: close\r\n\r\n" +
                      responseBody;
        WriteStringToStream(stream, http);
    }

    private string DispatchOnMainThread(string cmd, string arg, int timeoutMs = 2000)
    {
        string result = null;
        using ManualResetEventSlim done = new ManualResetEventSlim(false);

        void Callback()
        {
            try
            {
                result = dispatcher.Dispatch(cmd, arg);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayControl] Command execution error: {ex}");
                result = "error:command_exception";
            }
            finally
            {
                done.Set();
                EditorApplication.update -= Callback;
            }
        }

        EditorApplication.update += Callback;

        if (!done.Wait(timeoutMs))
        {
            EditorApplication.update -= Callback;
            Debug.LogWarning($"[RemotePlayControl] Command timeout for '{cmd}'");
            return "error:timeout";
        }

        return result ?? string.Empty;
    }

    private static (string command, string arg) ParseHttpCommand(string request)
    {
        using StringReader stringReader = new(request);
        string line = stringReader.ReadLine();
        if (string.IsNullOrEmpty(line))
        {
            return ("status", string.Empty);
        }

        string[] parts = line.Split(' ');
        if (parts.Length < 2)
        {
            return ("status", string.Empty);
        }

        string path = parts[1].TrimStart('/');
        if (string.IsNullOrEmpty(path))
        {
            return ("status", string.Empty);
        }

        string[] segs = path.Split(new[] { '/' }, 2);
        string cmd = segs[0].ToLowerInvariant();
        string arg = segs.Length > 1 ? WebUtility.UrlDecode(segs[1]) : string.Empty;
        return (cmd, arg);
    }

    private static string ReadStringFromStream(NetworkStream stream, int timeoutMs = 2000)
    {
        try
        {
            StringBuilder sb = new();
            byte[] buffer = new byte[1024];
            int start = Environment.TickCount;
            stream.ReadTimeout = timeoutMs;

            while (true)
            {
                int read;
                try
                {
                    read = stream.Read(buffer, 0, buffer.Length);
                }
                catch (IOException)
                {
                    break;
                }

                if (read <= 0)
                {
                    break;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, read));

                if (sb.ToString().Contains("\r\n\r\n"))
                {
                    break;
                }

                if (Environment.TickCount - start > timeoutMs)
                {
                    break;
                }

                if (!stream.DataAvailable)
                {
                    break;
                }
            }

            return sb.Length == 0 ? null : sb.ToString();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RemotePlayControl] Read error: {ex}");
            return null;
        }
    }

    private static void WriteStringToStream(NetworkStream stream, string text)
    {
        try
        {
            byte[] outBuf = Encoding.UTF8.GetBytes(text);
            stream.Write(outBuf, 0, outBuf.Length);
            stream.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RemotePlayControl] Write error: {ex}");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            running = false;
            try { listener?.Stop(); } catch { }
            try { listenerThread?.Join(1000); } catch { }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RemotePlayControl] Dispose error: {ex}");
        }

        if (disposing)
        {
            listener = null;
            listenerThread = null;
        }
    }

    ~TcpServer()
    {
        Dispose(false);
    }
}
