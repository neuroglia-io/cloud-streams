using k8s;
using k8s.Models;

namespace CloudStreams.Infrastructure;

/// <summary>
/// Exposes constants about Kubernetes resources required by Cloud Streams
/// </summary>
public static class KubernetesResources
{

    /// <summary>
    /// Exposes constants about Kubernetes Custom Resource Definitions required by Cloud Streams
    /// </summary>
    public static class ResourceDefinitions
    {

        /// <summary>
        /// Gets the <see cref="V1CustomResourceDefinition"/> for <see cref="Data.Models.Gateway"/>s
        /// </summary>
        public static readonly V1CustomResourceDefinition Gateway = LoadCustomResourceDefinition(nameof(Gateway));

        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> containing the <see cref="V1CustomResourceDefinition"/>s required by Cloud Streams
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<V1CustomResourceDefinition> AsEnumerable()
        {
            yield return Gateway;
        }

        static V1CustomResourceDefinition LoadCustomResourceDefinition(string name)
        {
            var resourceName = string.Join('.', typeof(KubernetesResources).Namespace, "Assets", "ResourceDefinitions", $"{name.ToLowerInvariant()}.yaml");
            using var stream = typeof(KubernetesResources).Assembly.GetManifestResourceStream(resourceName)!;
            using var streamReader = new StreamReader(stream);
            var yaml = streamReader.ReadToEnd();
            return KubernetesYaml.Deserialize<V1CustomResourceDefinition>(yaml)!;
        }

    }


}