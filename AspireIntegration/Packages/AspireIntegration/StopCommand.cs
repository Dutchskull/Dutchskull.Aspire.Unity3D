using UnityEditor;

internal class StopCommand : ICommand
{
    public string Execute(string argument)
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        };
        return "ok:stopped";
    }
}