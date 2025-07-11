namespace Henchman.IPC;

[AttributeUsage(AttributeTargets.Class)]
public class IPCAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
