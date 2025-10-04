using System;
using System.Collections.Generic;

internal static class CommandFactory
{
    public static ICommandDispatcher Create()
    {
        Dictionary<string, ICommand> map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["start"] = new StartCommand(),
            ["stop"] = new StopCommand(),
            ["toggle"] = new ToggleCommand(),
            ["status"] = new StatusCommand(),
            ["editor-health"] = new EditorHealthCommand(),
            ["playmode-health"] = new PlayModeHealthCommand(),
            ["config"] = new ConfigCommand()
        };

        UnknownCommand unknown = new();
        return new CommandDispatcher(map, unknown);
    }
}