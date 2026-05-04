namespace QmlSharp.Cli
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            return await QmlSharpCli.InvokeAsync(args, Console.Out, Console.Error).ConfigureAwait(false);
        }
    }
}
