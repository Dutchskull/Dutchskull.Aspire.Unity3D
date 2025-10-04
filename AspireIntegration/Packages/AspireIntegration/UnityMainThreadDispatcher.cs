using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _queue = new();
    private static UnityMainThreadDispatcher _instance;

    public static void Enqueue(Action a)
    {
        if (a == null)
        {
            return;
        }

        lock (_queue)
        {
            _queue.Enqueue(a);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Ensure()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject gameObject = new("UnityMainThreadDispatcher");
        DontDestroyOnLoad(gameObject);
        _instance = gameObject.AddComponent<UnityMainThreadDispatcher>();
    }

    private void Update()
    {
        while (true)
        {
            Action action = null;
            lock (_queue)
            {
                if (_queue.Count == 0)
                {
                    break;
                }

                action = _queue.Dequeue();
            }
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}