using System;
using System.Collections.Generic;

internal class CommandDispatcher : ICommandDispatcher
{
    private readonly IReadOnlyDictionary<string, ICommand> commands;
    private readonly ICommand defaultCommand;

    public CommandDispatcher(IDictionary<string, ICommand> commands, ICommand defaultCommand)
    {
        this.commands = new Dictionary<string, ICommand>(commands, StringComparer.OrdinalIgnoreCase);
        this.defaultCommand = defaultCommand;
    }

    public string Dispatch(string command, string arg)
    {
        return string.IsNullOrWhiteSpace(command)
            ? "error:empty_command"
            : commands.TryGetValue(command, out ICommand cmd) ? cmd.Execute(arg) : defaultCommand.Execute(arg);
    }
}