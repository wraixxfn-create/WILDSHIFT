namespace Wildshift.Core.Bootstrap
{
    /// <summary>Runtime lifecycle phases of the bootstrap sequence.</summary>
    public enum BootstrapPhase
    {
        NotStarted = 0,
        LoadingTargetScene = 1,
        Failed = 2
    }
}
