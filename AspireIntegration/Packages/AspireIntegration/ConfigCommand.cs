using Microsoft.Extensions.Configuration;
using System;
using UnityEditor;
using UnityEngine;

internal class ConfigCommand : ICommand
{
    public string Execute(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return "error:empty_body";
        }

        IConfigurationRoot configuration;
        try
        {
            ConfigsAsset asset = AssetDatabase.LoadAssetAtPath<ConfigsAsset>(ProjectConfigSettingsProvider.k_AssetPath);
            asset.externalJson = argument;
            configuration = ConfigProvider.BuildFromJson(argument);
        }
        catch (Exception ex)
        {
            return "error:invalid_json:" + ex.Message;
        }

        try
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    ConfigProvider.ReplaceConfiguration(configuration);
                    Debug.Log("[ConfigCommand] Configuration applied");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ConfigCommand] Failed to apply configuration: " + ex);
                }
            });
        }
        catch (Exception ex)
        {
            return "error:apply_failed:" + ex.Message;
        }

        return "ok";
    }
}