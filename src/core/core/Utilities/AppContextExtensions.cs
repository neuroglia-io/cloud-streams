namespace CloudStreams.Core;

/// <summary>
/// Exposes constants used to describe the current application
/// </summary>
public static class AppContextExtensions
{

    /// <summary>
    /// Indicates whether or not the application is running in Docker
    /// </summary>
    public static readonly bool RunsInDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    /// <summary>
    /// Indicates whether or not the application is running in Kubernetes
    /// </summary>
    public static readonly bool RunsInKubernetes = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST ") == "true";

}
