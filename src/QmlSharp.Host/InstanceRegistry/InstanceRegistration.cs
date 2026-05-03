namespace QmlSharp.Host.Instances
{
    /// <summary>Input metadata used when registering a managed ViewModel instance.</summary>
    public sealed record InstanceRegistration(
        string InstanceId,
        string ClassName,
        string SchemaId,
        string CompilerSlotKey,
        IntPtr NativeHandle,
        IntPtr RootObjectHandle);
}
