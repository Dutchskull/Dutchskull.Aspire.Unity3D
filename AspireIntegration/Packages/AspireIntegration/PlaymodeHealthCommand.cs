using UnityEditor;

internal class PlayModeHealthCommand : ICommand
{
    public string Execute(string argument)
    {
        return EditorApplication.isPlaying ? "healthy" : "unhealthy";
    }
}