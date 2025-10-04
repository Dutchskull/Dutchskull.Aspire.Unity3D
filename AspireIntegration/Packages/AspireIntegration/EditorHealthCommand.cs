using UnityEditor;

internal class EditorHealthCommand : ICommand
{
    public string Execute(string argument)
    {
        return "healthy";
    }
}