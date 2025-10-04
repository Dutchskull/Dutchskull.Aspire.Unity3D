using System.Collections.Generic;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;


[CreateAssetMenu(fileName = "ConfigsAsset", menuName = "Configs/ConfigsAsset")]
public class ConfigsAsset : ScriptableObject
{
    [TextArea(4, 20)]
    public string projectSettingsJson;

    [TextArea(4, 20)]
    public string externalJson;
}

static class ProjectConfigSettingsProvider
{
    private const string k_Path = "Project/Config";
    public const string k_AssetPath = "Assets/Config/ConfigsAsset.asset";

    [SettingsProvider]
    public static SettingsProvider CreateProvider() =>
        new(k_Path, SettingsScope.Project)
        {
            label = "Config",
            guiHandler = (search) => { OnGUI(); },
            keywords = new HashSet<string> { "config", "settings", "json" }
        };

    private static void OnGUI()
    {
        ConfigsAsset asset = GetOrCreateAsset();
        if (asset == null)
        {
            EditorGUILayout.HelpBox("Could not create or load ConfigsAsset.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Project Settings JSON", EditorStyles.boldLabel);
        asset.projectSettingsJson = EditorGUILayout.TextArea(asset.projectSettingsJson, GUILayout.Height(150));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("External JSON (for testing)", EditorStyles.boldLabel);
        asset.externalJson = EditorGUILayout.TextArea(asset.externalJson, GUILayout.Height(150));

        if (GUI.changed)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            ConfigsEditorUtility.ApplyMergedConfig(asset);
        }

        if (GUILayout.Button("Apply merged config now"))
        {
            ConfigsEditorUtility.ApplyMergedConfig(asset);
        }
    }

    private static ConfigsAsset GetOrCreateAsset()
    {
        ConfigsAsset asset = AssetDatabase.LoadAssetAtPath<ConfigsAsset>(k_AssetPath);
        if (asset == null)
        {
            string dir = System.IO.Path.GetDirectoryName(k_AssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets", "Config");
            }

            asset = ScriptableObject.CreateInstance<ConfigsAsset>();
            AssetDatabase.CreateAsset(asset, k_AssetPath);
            AssetDatabase.SaveAssets();
        }
        return asset;
    }
}

public static class ConfigsEditorUtility
{
    private const string k_AssetPath = "Assets/Config/ConfigsAsset.asset";

    public static void ApplyMergedConfig(ConfigsAsset asset = null)
    {
        if (asset == null)
        {
            asset = AssetDatabase.LoadAssetAtPath<ConfigsAsset>(k_AssetPath);
        }

        if (asset == null)
        {
            return;
        }

        string merged = MergeJson(asset.projectSettingsJson, asset.externalJson);
        Microsoft.Extensions.Configuration.IConfigurationRoot cfg = ConfigProvider.BuildFromJson(merged);
        ConfigProvider.ReplaceConfiguration(cfg);
    }

    private static string MergeJson(string baseJson, string overrideJson)
    {
        if (string.IsNullOrWhiteSpace(baseJson))
        {
            baseJson = "{}";
        }

        if (string.IsNullOrWhiteSpace(overrideJson))
        {
            return baseJson;
        }

        JObject baseJ = JObject.Parse(baseJson);
        JObject overJ = JObject.Parse(overrideJson);
        baseJ.Merge(overJ, new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            MergeNullValueHandling = MergeNullValueHandling.Merge
        });
        return baseJ.ToString();
    }
}