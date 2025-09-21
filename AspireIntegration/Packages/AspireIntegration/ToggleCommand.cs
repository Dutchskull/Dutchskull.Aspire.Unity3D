using UnityEditor;

internal class ToggleCommand : ICommand
{
    public string Execute(string argument)
    {
        EditorApplication.delayCall += () =>
        {
            EditorApplication.isPlaying = !EditorApplication.isPlaying;
        };
        return "ok:toggled";
    }
}