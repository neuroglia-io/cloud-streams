using Microsoft.Extensions.Hosting;
using System.Reactive.Subjects;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents a file-based <see cref="IResourceRepository"/> implementation
/// </summary>
public class FileBasedResourceRepository
    : BackgroundService, IResourceRepository
{

    /// <summary>
    /// Gets the service used to watch resource files
    /// </summary>
    protected FileSystemWatcher FileWatcher { get; private set; }

    protected Subject<JsonObject> x { get; }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.FileWatcher = new FileSystemWatcher();
        this.FileWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        this.FileWatcher.Filter = "*.*";
        this.FileWatcher.EnableRaisingEvents = true;
        this.FileWatcher.Changed += OnResourceFileChanged;
        this.FileWatcher.Created += OnResourceFileChanged;
        this.FileWatcher.Deleted += OnResourceFileChanged;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> AddResourceAsync<TResource>(TResource resource, CancellationToken cancellationToken)
        where TResource : class, IResource, new()
    {

    }

    /// <inheritdoc/>
    public virtual async Task<TResource?> GetResourceAsync<TResource>(string name, string? @namespace, CancellationToken cancellationToken)
        where TResource : class, IResource, new()
    {

    }

    /// <inheritdoc/>
    public virtual async Task<IList<TResource>?> ListResourcesAsync<TResource>(string? @namespace, CancellationToken cancellationToken)
        where TResource : class, IResource, new()
    {
 
    }

    /// <inheritdoc/>
    public virtual async Task<IObservable<IResourceWatchEvent<TResource>>> WatchResourcesAsync<TResource>(string? @namespace, CancellationToken cancellationToken)
        where TResource : class, IResource, new()
    {

    }

    protected virtual void OnResourceFileChanged(object? sender, FileSystemEventArgs e)
    {
        var file = new FileInfo(e.FullPath);
        Resource? resource = null;
        if (file.Exists)
        {
            if (file.Extension.EndsWith(".json")) resource = Serializer.Json.Deserialize<Resource>(file.FullName);
            else if (file.Extension.EndsWith(".yaml") || file.Extension.EndsWith(".yml")) resource = Serializer.Yaml.Deserialize<Resource>(file.FullName);
            else return;
        }

    }

}
