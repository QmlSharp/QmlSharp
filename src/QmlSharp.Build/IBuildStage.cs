namespace QmlSharp.Build
{
    internal interface IBuildStage
    {
        BuildPhase Phase { get; }

        Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken);
    }
}
