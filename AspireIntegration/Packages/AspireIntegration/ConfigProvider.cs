using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text;

public static class ConfigProvider
{
    private static readonly object _lock = new();
    private static IConfigurationRoot _configuration;

    public static event Action<IConfigurationRoot> OnConfigurationReloaded;

    public static IConfigurationRoot Configuration
    {
        get
        {
            lock (_lock)
            {
                return _configuration;
            }
        }
    }

    public static IConfigurationRoot BuildFromJson(string json)
    {
        if (json == null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        ConfigurationBuilder builder = new();
        using MemoryStream ms = new(Encoding.UTF8.GetBytes(json));
        builder.AddJsonStream(ms);
        return builder.Build();
    }

    public static void ReplaceConfiguration(IConfigurationRoot newConfiguration)
    {
        if (newConfiguration == null)
        {
            throw new ArgumentNullException(nameof(newConfiguration));
        }

        lock (_lock)
        {
            _configuration = newConfiguration;
        }

        OnConfigurationReloaded?.Invoke(newConfiguration);
    }
}