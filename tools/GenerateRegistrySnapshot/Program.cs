namespace QmlSharp.Tools.GenerateRegistrySnapshot
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return RegistrySnapshotGeneratorCommand.Run(args, Console.Out, Console.Error);
        }
    }
}
