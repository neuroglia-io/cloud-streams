using CloudStreams.Core.Data.Models;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the <see href="https://kubernetes.io">Kubernetes</see>-based implementation of the <see cref="IResourceRepository"/> interface
/// </summary>
public class K8sResourceRepository
    : BackgroundService, IResourceRepository
{

    private TaskCompletionSource? _InitializationCompletionSource;

    /// <summary>
    /// Initializes a new <see cref="K8sResourceRepository"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="kubernetes"></param>
    public K8sResourceRepository(ILoggerFactory loggerFactory, Kubernetes kubernetes)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.Kubernetes = kubernetes;
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to interact with the Kubernetes API
    /// </summary>
    protected Kubernetes Kubernetes { get; }

    /// <summary>
    /// Gets a boolean indicating whether or not the <see cref="K8sResourceRepository"/> has been initialized
    /// </summary>
    protected bool Initialized { get; private set; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._InitializationCompletionSource = new();
        IList<Gateway>? gateways = null;
        try
        {
            gateways = (await this.Kubernetes.ListClusterCustomObjectAsync<Gateway>(stoppingToken).ConfigureAwait(false))?.Items;
        }
        catch { }
        if(gateways == null || !gateways.Any()) await this.SeedAsync(stoppingToken).ConfigureAwait(false);
        this.Initialized = true;
        this._InitializationCompletionSource.SetResult();
        this._InitializationCompletionSource = null;
    }

    /// <summary>
    /// Seeds the <see cref="K8sResourceRepository"/>
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach(var resourceDefinition in KubernetesResources.ResourceDefinitions.AsEnumerable())
        {
            try
            {
                await this.Kubernetes.CreateCustomResourceDefinitionAsync(resourceDefinition, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                this.Logger.LogError("An error occured while seeding the resource repository: {ex}", ex);
            }
        }
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> CreateResourceAsync<TResource>(TResource resource, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        await this.WaitUntilInitializedAsync();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        JsonElement? resourceObject;
        if(string.IsNullOrWhiteSpace(resource.Metadata.Namespace)) resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.CreateClusterCustomObjectAsync(resource, group, version, plural, cancellationToken: cancellationToken);
        else resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.CreateNamespacedCustomObjectAsync(resource, group, version, resource.Metadata.Namespace, plural, cancellationToken: cancellationToken);
        return Serializer.Json.Deserialize<TResource?>((JsonElement)resourceObject)!;
    }

    /// <inheritdoc/>
    public virtual async Task<IResourceDefinition?> GetResourceDefinitionAsync<TResource>(CancellationToken cancellationToken)
         where TResource : class, IResource, new()
    {
        await this.WaitUntilInitializedAsync();
        var resource = new TResource();
        var definition = await this.Kubernetes.ReadCustomResourceDefinitionAsync(resource.Type.ToString(), cancellationToken: cancellationToken);
        if(definition == null) return null;
        return Serializer.Json.Deserialize<ResourceDefinition>(Serializer.Json.Serialize(definition));
    }

    /// <inheritdoc/>
    public virtual async Task<TResource?> GetResourceAsync<TResource>(string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        await this.WaitUntilInitializedAsync();
        var resource = new TResource();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        JsonElement? resourceObject;
        try
        {
            if (string.IsNullOrWhiteSpace(@namespace)) resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.GetClusterCustomObjectAsync(group, version, plural, name, cancellationToken).ConfigureAwait(false);
            else resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.GetNamespacedCustomObjectAsync(group, version, @namespace, plural, name, cancellationToken).ConfigureAwait(false);
            if (resourceObject == null) return null;
        }
        catch(HttpOperationException ex)when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        return Serializer.Json.Deserialize<TResource?>((JsonElement)resourceObject);
    }

    /// <inheritdoc/>
    public virtual async Task<IAsyncEnumerable<TResource>?> ListResourcesAsync<TResource>(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        await this.WaitUntilInitializedAsync();
        var resource = new TResource();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        var labelSelector = labelSelectors?.Select(l => l.ToString()).Join(',');
        JsonElement? resourceObjectArray;
        if (string.IsNullOrWhiteSpace(@namespace)) resourceObjectArray = (JsonElement)await this.Kubernetes.CustomObjects.ListClusterCustomObjectAsync(group, version, plural, labelSelector: labelSelector, cancellationToken: cancellationToken).ConfigureAwait(false);
        else resourceObjectArray = (JsonElement)await this.Kubernetes.CustomObjects.ListNamespacedCustomObjectAsync(group, version, @namespace, plural, labelSelector: labelSelector, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (resourceObjectArray == null) return null;
        return Serializer.Json.Deserialize<CustomResourceList<TResource>>((JsonElement)resourceObjectArray)?.Items.ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> UpdateResourceAsync<TResource>(TResource resource, CancellationToken cancellationToken)
        where TResource : class, IResource, new()
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        await this.WaitUntilInitializedAsync();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        JsonElement? resourceObject;
        if (string.IsNullOrWhiteSpace(resource.Metadata.Namespace)) resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.ReplaceClusterCustomObjectAsync(resource, group, version, plural, resource.GetName(), cancellationToken: cancellationToken);
        else resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.ReplaceNamespacedCustomObjectAsync(resource, group, version, resource.GetNamespace(), plural, resource.GetName(), cancellationToken: cancellationToken);
        return Serializer.Json.Deserialize<TResource?>((JsonElement)resourceObject)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> UpdateResourceStatusAsync<TResource>(TResource resource, CancellationToken cancellationToken)
        where TResource : class, IResource, new()
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        await this.WaitUntilInitializedAsync();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        JsonElement? resourceObject;
        if (string.IsNullOrWhiteSpace(resource.Metadata.Namespace)) resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.ReplaceClusterCustomObjectStatusAsync(resource, group, version, plural, resource.GetName(), cancellationToken: cancellationToken);
        else resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.ReplaceNamespacedCustomObjectStatusAsync(resource, group, version, resource.GetNamespace(), plural, resource.GetName(), cancellationToken: cancellationToken);
        return Serializer.Json.Deserialize<TResource?>((JsonElement)resourceObject)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource?> PatchResourceAsync<TResource>(Patch patch, string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        if (patch == null) throw new ArgumentNullException(nameof(patch));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        await this.WaitUntilInitializedAsync();
        var resource = new TResource();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        JsonElement? resourceObject;
        var k8sPatchType = patch.Type switch
        {
            PatchType.JsonMergePatch => V1Patch.PatchType.MergePatch,
            PatchType.JsonPatch => V1Patch.PatchType.JsonPatch,
            PatchType.StrategicMergePatch => V1Patch.PatchType.StrategicMergePatch,
            _ => throw new NotSupportedException($"The specified patch type '{patch.Type}' is not supported")
        };
        var k8sPatch = new V1Patch(patch.Document, k8sPatchType);
        if (string.IsNullOrWhiteSpace(@namespace)) resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.PatchClusterCustomObjectAsync(k8sPatch, group, version, plural, name, cancellationToken: cancellationToken).ConfigureAwait(false);
        else resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.PatchNamespacedCustomObjectAsync(k8sPatch, group, version, @namespace, plural, name, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<TResource>((JsonElement)resourceObject);
    }

    /// <inheritdoc/>
    public virtual async Task<TResource?> PatchResourceStatusAsync<TResource>(Patch patch, string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        if (patch == null) throw new ArgumentNullException(nameof(patch));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        await this.WaitUntilInitializedAsync();
        var resource = new TResource();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        JsonElement? resourceObject;
        var k8sPatchType = patch.Type switch
        {
            PatchType.JsonMergePatch => V1Patch.PatchType.MergePatch,
            PatchType.JsonPatch => V1Patch.PatchType.JsonPatch,
            PatchType.StrategicMergePatch => V1Patch.PatchType.StrategicMergePatch,
            _ => throw new NotSupportedException($"The specified patch type '{patch.Type}' is not supported")
        };
        var k8sPatch = new V1Patch(patch.Document, k8sPatchType);
        if (string.IsNullOrWhiteSpace(@namespace)) resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.PatchClusterCustomObjectStatusAsync(k8sPatch, group, version, plural, name, cancellationToken: cancellationToken).ConfigureAwait(false);
        else resourceObject = (JsonElement)await this.Kubernetes.CustomObjects.PatchNamespacedCustomObjectStatusAsync(k8sPatch, group, version, @namespace, plural, name, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<TResource>((JsonElement)resourceObject);
    }

    /// <inheritdoc/>
    public virtual async Task<IObservable<IResourceWatchEvent<TResource>>> WatchResourcesAsync<TResource>(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        await this.WaitUntilInitializedAsync();
        var resource = new TResource();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        var labelSelector = labelSelectors?.Select(l => l.ToString()).Join(',');
        HttpOperationResponse<object> response;
        if (string.IsNullOrWhiteSpace(@namespace)) response = await this.Kubernetes.CustomObjects.ListClusterCustomObjectWithHttpMessagesAsync(group, version, plural, watch: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        else response = await this.Kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync(group, version, @namespace, plural, watch: true, labelSelector: labelSelector, cancellationToken: cancellationToken).ConfigureAwait(false);
        var subject = new Subject<IResourceWatchEvent<TResource>>();
        var watch = response.Watch((WatchEventType watchEventType, TResource resource) => subject.OnNext(new ResourceWatchEvent<TResource>(watchEventType.ToCloudStreamsEventType(), resource)));
        return Observable.Using
        (
            () => watch, 
            watch => subject
        );
    }

    /// <inheritdoc/>
    public virtual async Task DeleteResourceAsync<TResource>(string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        await this.WaitUntilInitializedAsync();
        var resource = new TResource();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        if (string.IsNullOrWhiteSpace(@namespace)) await this.Kubernetes.CustomObjects.DeleteClusterCustomObjectAsync(group, version, plural, name, cancellationToken: cancellationToken).ConfigureAwait(false);
        else await this.Kubernetes.CustomObjects.DeleteNamespacedCustomObjectAsync(group, version, @namespace, plural, name, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for the <see cref="K8sResourceRepository"/> to be initialized
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task WaitUntilInitializedAsync()
    {
        if (this.Initialized) return;
        if (this._InitializationCompletionSource == null) await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        if (this._InitializationCompletionSource != null) await this._InitializationCompletionSource!.Task.ConfigureAwait(false);
    }

}
