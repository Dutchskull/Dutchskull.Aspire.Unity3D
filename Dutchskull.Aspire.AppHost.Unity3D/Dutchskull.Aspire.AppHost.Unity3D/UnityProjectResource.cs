using Aspire.Hosting.ApplicationModel;

namespace Dutchskull.Aspire.Unity3D.Hosting;

public sealed class UnityProjectResource : Resource, IResourceWithEndpoints {
    public UnityProjectResource(string name, string unityExePath, string projectPath, Uri controlUrl)
        : base(name) {
        UnityExePath = unityExePath ?? throw new ArgumentNullException(nameof(unityExePath));
        ProjectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
        ControlUrl = controlUrl ?? throw new ArgumentNullException(nameof(controlUrl));
    }

    public Uri ControlUrl { get; }

    public string ProjectPath { get; }

    public string UnityExePath { get; }
}