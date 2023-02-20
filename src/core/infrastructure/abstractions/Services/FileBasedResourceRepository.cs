using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Reactive.Subjects;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents a file system based <see cref="IResourceRepository"/> implementation
/// </summary>
public class FileBasedResourceRepository
    : BackgroundService, IResourceRepository
{

    private static readonly IEnumerable<string> SupportedFileExtensions = new string[] { "*.json", "*.yaml", "*.yml" };

    /// <summary>
    /// Initializes a new <see cref="FileBasedResourceRepository"/>
    /// </summary>
    /// <param name="options">The service used to access the current <see cref="FileBasedResourceRepositoryOptions"/></param>
    public FileBasedResourceRepository(IOptions<FileBasedResourceRepositoryOptions> options)
    {
        this.Options = options.Value;
        this.Subject = new();
    }

    /// <summary>
    /// Gets the current <see cref="FileBasedResourceRepositoryOptions"/>
    /// </summary>
    protected FileBasedResourceRepositoryOptions Options { get; }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to observe and emit <see cref="IResourceWatchEvent"/>s
    /// </summary>
    protected Subject<IResourceWatchEvent> Subject { get; }

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> used to cache managed <see cref="IResource"/>s
    /// </summary>
    protected ConcurrentDictionary<string, FileBasedResourceDescriptor> Cache { get; } = new();

    /// <summary>
    /// Gets the service used to watch resource files
    /// </summary>
    protected FileSystemWatcher FileWatcher { get; private set; } = null!;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directory = new DirectoryInfo(this.Options.Directory);
        if (!directory.Exists) directory.Create();
        foreach(var file in SupportedFileExtensions.SelectMany(ex => directory.GetFiles(ex, SearchOption.AllDirectories)))
        {
            var content = await File.ReadAllTextAsync(file.FullName, stoppingToken).ConfigureAwait(false);
            Resource? resource = null;
            if (file.IsJson()) resource = Serializer.Json.Deserialize<Resource>(content);
            else if (file.IsYaml()) resource = Serializer.Yaml.Deserialize<Resource>(content);
            if(resource == null) continue;
            var resourceDescriptor = new FileBasedResourceDescriptor(file.FullName, file.CreationTime, file.LastWriteTime, resource);
            var key = this.GetResourceCacheKey(resource.ApiVersion, resource.Kind, resource.Metadata.Name!, resource.Metadata.Namespace);
            this.Cache.AddOrUpdate(key, resourceDescriptor, (_, _) => resourceDescriptor);
        }
        this.FileWatcher = new FileSystemWatcher();
        this.FileWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        this.FileWatcher.Filter = "*.*";
        this.FileWatcher.Changed += OnResourceFileChanged;
        this.FileWatcher.Created += OnResourceFileChanged;
        this.FileWatcher.Deleted += OnResourceFileChanged;
        this.FileWatcher.EnableRaisingEvents = true;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> AddResourceAsync<TResource>(TResource resource, CancellationToken cancellationToken)
        where TResource : class, IResource, new()
    {
        var content = Serializer.Json.Serialize(resource);
        var file = new FileInfo(this.GetResourceFilePath(resource.ApiVersion, resource.Kind, resource.GetName(), resource.GetNamespace()));
        using var stream = file.Exists ? file.OpenRead() : file.Create();
        using var streamWriter = new StreamWriter(stream);
        await streamWriter.WriteAsync(content);
        await streamWriter.FlushAsync();
        return resource;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource?> GetResourceAsync<TResource>(string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        var resourceFile = new FileInfo(this.GetResourceFilePath(resource.ApiVersion, resource.Kind, name, @namespace));
        if (!resourceFile.Exists) return null;
        using var stream = resourceFile.OpenRead();
        using var streamReader = new StreamReader(stream);
        var content = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if(resourceFile.IsJson()) return Serializer.Json.Deserialize<TResource>(content);
        else return Serializer.Yaml.Deserialize<TResource>(content);
    }

    /// <inheritdoc/>
    public virtual Task<IAsyncEnumerable<TResource>?> ListResourcesAsync<TResource>(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        var keyPattern = $"{resource.ApiVersion}/{resource.Kind}";
        if (!string.IsNullOrWhiteSpace(@namespace)) keyPattern += $"namespace/{@namespace}";
        return Task.FromResult(this.Cache.Keys
            .Where(k => k.StartsWith(keyPattern))
            .Select(k => Serializer.Json.Deserialize<TResource>(Serializer.Json.Serialize(this.Cache[k]!.State))!)
            .ToAsyncEnumerable())!;
    }

    /// <inheritdoc/>
    public virtual Task<IObservable<IResourceWatchEvent<TResource>>> WatchResourcesAsync<TResource>(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        return Task.FromResult(this.Subject.Where(e => e.Resource.IsOfType<TResource>()).Select(e => e.ToType<TResource>()));
    }

    /// <summary>
    /// Builds a new cache key for the specified resource
    /// </summary>
    /// <param name="apiVersion">The API version the resource to get the cache key for belongs to</param>
    /// <param name="kind">The kind of the resource to get the cache key for</param>
    /// <param name="name">The name of the resource to get the cache key for</param>
    /// <param name="namespace">The namespace the resource to get the cache key for belongs to</param>
    /// <returns>The specified resource's cache key</returns>
    protected virtual string GetResourceCacheKey(string apiVersion, string kind, string name, string? @namespace)
    {
        if (string.IsNullOrWhiteSpace(apiVersion)) throw new ArgumentNullException(nameof(apiVersion));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentNullException(nameof(kind));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(@namespace)) return $"{apiVersion}/{kind}/global/{name}";
        else return $"{apiVersion}/{kind}/namespace/{@namespace}/{name}";
    }

    /// <summary>
    /// Builds a new cache key for the specified resource
    /// </summary>
    /// <typeparam name="TResource">The type of the resource to build a new cache key for</typeparam>
    /// <param name="name">The name of the resource to build a new cache key for</param>
    /// <param name="namespace">The namespace the resource to build a new cache key for belongs to</param>
    /// <returns>A new cache key</returns>
    protected virtual string GetResourceCacheKey<TResource>(string name, string? @namespace)
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        return this.GetResourceCacheKey(resource.ApiVersion, resource.Kind, name, @namespace);
    }

    /// <summary>
    /// Gets the specified <see cref="IResource"/>'s file path
    /// </summary>
    /// <param name="apiVersion">The API version the resource to get the file path for belongs to</param>
    /// <param name="kind">The kind of the resource to get the file path for</param>
    /// <param name="name">The name of the resource to get the file path for</param>
    /// <param name="namespace">The namespace the resource to get the file path for belongs to</param>
    /// <returns></returns>
    protected virtual string GetResourceFilePath(string apiVersion, string kind, string name, string? @namespace)
    {
        if (string.IsNullOrWhiteSpace(apiVersion)) throw new ArgumentNullException(nameof(apiVersion));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentNullException(nameof(kind));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(@namespace)) return Path.Combine(AppContext.BaseDirectory, Path.Combine(apiVersion.Split('/')), "global", name);
        else return Path.Combine(AppContext.BaseDirectory, Path.Combine(apiVersion.Split('/')), "namespaces", @namespace, name);
    }

    /// <summary>
    /// Handles the event fired whenever a watched file changes
    /// </summary>
    /// <param name="sender">The sender of the event</param>
    /// <param name="e">The event's arguments</param>
    protected virtual void OnResourceFileChanged(object? sender, FileSystemEventArgs e)
    {
        var file = new FileInfo(e.FullPath);
        if (!SupportedFileExtensions.Contains(file.Extension.ToLowerInvariant())) return;
        Resource? resource = null;
        if (file.Exists)
        {
            if (file.IsJson()) resource = Serializer.Json.Deserialize<Resource>(file.FullName);
            else if (file.IsYaml()) resource = Serializer.Yaml.Deserialize<Resource>(file.FullName);
            else return;
        }
        if (resource == null) return;
        switch(e.ChangeType)
        {
            case WatcherChangeTypes.Created:
                this.Subject.OnNext(new ResourceWatchEvent(ResourceWatchEventType.Created, resource));
                break;
            case WatcherChangeTypes.Changed:
                this.Subject.OnNext(new ResourceWatchEvent(ResourceWatchEventType.Updated, resource));
                break;
            case WatcherChangeTypes.Deleted:
                this.Subject.OnNext(new ResourceWatchEvent(ResourceWatchEventType.Deleted, resource));
                break;
        }
    }

    /// <summary>
    /// Represents an object used to describe a file-based <see cref="IResource"/>
    /// </summary>
    protected class FileBasedResourceDescriptor
    {

        /// <summary>
        /// Initializes a new <see cref="FileBasedResourceDescriptor"/>
        /// </summary>
        /// <param name="filePath">The path to the <see cref="IResource"/> file</param>
        /// <param name="createdAt">The date and time at which the <see cref="IResource"/> file has been created</param>
        /// <param name="lastModified">The date and time at which the <see cref="IResource"/> file has last been modified</param>
        /// <param name="state">The current state of the <see cref="IResource"/></param>
        public FileBasedResourceDescriptor(string filePath, DateTimeOffset createdAt, DateTimeOffset lastModified, IResource state)
        {
            this.FilePath = filePath;
            this.CreatedAt = createdAt;
            this.LastModified = lastModified;
            this.State = state;
        }

        /// <summary>
        /// Gets the path to the <see cref="IResource"/> file
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the date and time at which the <see cref="IResource"/> file has been created
        /// </summary>
        public virtual DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets the date and time at which the <see cref="IResource"/> file has last been modified
        /// </summary>
        public virtual DateTimeOffset LastModified { get; }

        /// <summary>
        /// Gets the current state of the <see cref="IResource"/>
        /// </summary>
        public virtual IResource State { get; }

    }

}

/// <summary>
/// Represents the options used to configure a <see cref="FileBasedResourceRepository"/>
/// </summary>
public class FileBasedResourceRepositoryOptions
{

    /// <summary>
    /// Gets/sets the path to the directory to store resources into
    /// </summary>
    public virtual string Directory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Data", "Resources");

}