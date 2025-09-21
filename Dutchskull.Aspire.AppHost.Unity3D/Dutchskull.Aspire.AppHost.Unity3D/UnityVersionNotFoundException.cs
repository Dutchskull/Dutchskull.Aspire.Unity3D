namespace Dutchskull.Aspire.Unity3D.Hosting;

[Serializable]
internal class UnityVersionNotFoundException : Exception
{
    public UnityVersionNotFoundException()
    {
    }

    public UnityVersionNotFoundException(string? message) : base(message)
    {
    }

    public UnityVersionNotFoundException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}