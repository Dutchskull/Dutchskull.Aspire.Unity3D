using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal class StartCommand : ICommand
{
    public string Execute(string scene)
    {
        try
        {
            string scenePath = ResolveScenePath(scene);
            if (string.IsNullOrEmpty(scenePath))
            {
                return "error:scene_not_found";
            }

            if (!IsSceneAlreadyOpen(scenePath))
            {
                Scene openedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!openedScene.IsValid())
                {
                    return "error:open_scene_failed";
                }
            }

            EnterPlayModeIfNeeded();

            return "ok:started";
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RemotePlayControl] StartCommand error: {exception}");
            return "error:start_failed";
        }
    }

    private void EnterPlayModeIfNeeded()
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
    }

    private bool IsSceneAlreadyOpen(string scenePath)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene openScene = SceneManager.GetSceneAt(i);
            if (string.Equals(openScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string ResolveScenePath(string scene)
    {
        if (int.TryParse(scene, out int buildIndex))
        {
            if (buildIndex < 0)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene currentScene = SceneManager.GetSceneAt(i);
                    if (currentScene.isLoaded && !string.IsNullOrEmpty(currentScene.path))
                    {
                        return currentScene.path;
                    }
                }
                return null;
            }

            return buildIndex >= SceneManager.sceneCountInBuildSettings
                ? null
                : SceneUtility.GetScenePathByBuildIndex(buildIndex);
        }

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(name, scene, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, scene, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return scene;
    }
}