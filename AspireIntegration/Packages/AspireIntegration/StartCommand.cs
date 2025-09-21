using System;
using UnityEditor;
using UnityEngine;

internal class StartCommand : ICommand
{
    public string Execute(string argument)
    {
        try
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
#if UNITY_2019_3_OR_NEWER
                EditorApplication.EnterPlaymode();
#else
                EditorApplication.ExecuteMenuItem("Edit/Play");
                if (!EditorApplication.isPlaying)
                    EditorApplication.isPlaying = true;
#endif
            }

            return "ok:started";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RemotePlayControl] StartCommand error: {ex}");
            return "error:start_failed";
        }
    }
}
