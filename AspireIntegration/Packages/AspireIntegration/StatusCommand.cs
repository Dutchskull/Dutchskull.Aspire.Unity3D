using UnityEditor;

internal class StatusCommand : ICommand
{
    public string Execute(string argument)
    {
        return EditorApplication.isPlaying ? "status:playing" : "status:stopped";
    }
}