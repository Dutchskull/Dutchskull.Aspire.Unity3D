internal class UnknownCommand : ICommand
{
    public string Execute(string argument) => "error:unknown_command";
}