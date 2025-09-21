internal interface ICommandDispatcher
{
    string Dispatch(string command, string arg);
}