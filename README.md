# ⚠️ Experimental Project

This integration is **highly experimental**. It may change, break, or be abandoned at any time.
Use at your own risk — there are no guarantees that development will continue.

# Aspire Unity Integration

A [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/overview/what-is-dotnet-aspire) **AppHost extension** for integrating and controlling **Unity3D projects**.

This library makes it possible to bind Unity projects into your Aspire-powered applications.
Currently, the integration focuses on **starting and stopping Unity** as part of your AppHost lifecycle. Future features will expand support for **passing environment variables** and deeper communication between Aspire services and Unity.

## ✨ Features

* 🔗 Add a Unity3D project to your Aspire AppHost.
* ▶️ Automatically start Unity when the AppHost starts.
* ⏹ Stop Unity when the AppHost stops.
* 🛠 Future support planned for environment variable injection and richer integration.

## 📦 Installation

### 1. Add the .NET Aspire Unity package to your **AppHost** project

```bash
dotnet add package Dutchskull.Aspire.Unity
```

### 2. Add the Unity side via Unity’s Package Manager

Open your Unity project and add the following Git-based dependency:

```text
https://github.com/Dutchskull/Aspire.Unity3D.git?path=/AspireIntegration/Packages/AspireIntegration
```

Here’s how you could present it in the README with the updated signature:

---

## 🚀 Usage

### Basic example

If you just want to add a Unity project with defaults:

```csharp
var unity = builder.AddUnityProject("game", "..\\..\\AspireIntegration");
```

This will:

* Register a Unity project named **`game`**.
* Use the relative path `..\\..\\AspireIntegration` as the Unity project folder.
* Start Unity on the default URL `http://127.0.0.1` and port `54021`.

### Optional arguments

The `AddUnityProject` method also supports additional parameters if you need more control:

```csharp
public static IResourceBuilder<UnityProjectResource> AddUnityProject(
    this IDistributedApplicationBuilder builder,
    string name,
    string projectPath,
    int? sceneIndex = null,
    string url = "http://127.0.0.1",
    int port = 54021,
    string? customUnityInstallRoot = null) {...}

public static IResourceBuilder<UnityProjectResource> AddUnityProject(
    this IDistributedApplicationBuilder builder,
    string name,
    string projectPath,
    string? sceneName = null,
    string url = "http://127.0.0.1",
    int port = 54021,
    string? customUnityInstallRoot = null) {...}
```

* **`sceneName/sceneIndex`** → You can pass the scene index or name that should be playing in unity when starting the AppHost. Make sure it exists in the build settings.
* **`url`** → The base URL where the Unity project will be hosted (default: `http://127.0.0.1`) (Not supported to change yet).
* **`port`** → The port Unity should listen on (default: `54021`) (Not supported to change yet).
* **`customUnityInstallRoot`** → (optional) Path to a custom Unity installation if you don’t want to use the default Unity install.

## 🔮 Roadmap

Planned improvements include:

* Passing **environment variables** into Unity.
* Richer communication between Aspire and Unity (logs).

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or submit pull requests with improvements, bug fixes, or new features.

## 📜 License

MIT License © [Dutchskull](https://github.com/Dutchskull)