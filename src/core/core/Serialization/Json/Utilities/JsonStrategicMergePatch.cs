namespace CloudStreams.Serialization.Json.Utilities;

/// <summary>
/// Provides functionality to handle <see href="https://github.com/kubernetes/community/blob/master/contributors/devel/sig-api-machinery/strategic-merge-patch.md">Json Strategic Merge Patches</see>
/// </summary>
public static class JsonStrategicMergePatch
{

    /// <summary>
    /// Patches the specified <see cref="JsonNode"/>
    /// </summary>
    /// <param name="target">The <see cref="JsonNode"/> to patch</param>
    /// <param name="patch">The patch to apply</param>
    /// <param name="schema">The <see cref="JsonSchema"/> that describes the object to patch</param>
    /// <param name="patchConfiguration">An object used to configure the <see cref="JsonStrategicMergePatch"/></param>
    /// <returns>The patched <see cref="JsonNode"/></returns>
    public static JsonNode? ApplyPatch(JsonNode? target, JsonNode? patch, JsonSchema? schema = null, Configuration? patchConfiguration = null)
    {
        if (patch == null) return target;
        return patch switch
        {
            JsonArray arrayPatch => ApplyPatch(target?.AsArray(), arrayPatch, schema, patchConfiguration),
            JsonObject objectPatch => ApplyPatch(target?.AsObject(), objectPatch, schema, patchConfiguration),
            JsonValue valuePatch => valuePatch.Copy(),
            _ => null,
        };
    }

    /// <summary>
    /// Patches the specified <see cref="JsonObject"/>
    /// </summary>
    /// <param name="target">The <see cref="JsonObject"/> to patch</param>
    /// <param name="patch">The patch to apply</param>
    /// <param name="schema">The <see cref="JsonObject"/> that describes the object to patch</param>
    /// <param name="patchConfiguration">An object used to configure the <see cref="JsonStrategicMergePatch"/></param>
    /// <returns>The patched <see cref="JsonObject"/></returns>
    public static JsonObject? ApplyPatch(JsonObject? target, JsonObject? patch, JsonSchema? schema = null, Configuration? patchConfiguration = null)
    {
        if (patch == null) return target;
        if (patchConfiguration == null) patchConfiguration = JsonSerializer.Deserialize<Configuration>(patch);
        var patched = target.Copy()?.AsObject();
        patched ??= new();
        switch (patchConfiguration?.Strategy)
        {
            case Strategies.Delete: return null;
            case Strategies.Merge:
            case Strategies.Replace:
            default:
                if (patchConfiguration?.Strategy == Strategies.Replace)
                {
                    foreach (var propertyKey in patched.Select(p => p.Key).ToList())
                    {
                        if (patchConfiguration?.RetainKeys?.Contains(propertyKey) == true) continue;
                        patched.Remove(propertyKey);
                    }
                }
                foreach (var propertyPatch in patch.Where(p => !Directives.AsEnumerable().Contains(p.Key)))
                {
                    patched.TryGetPropertyValue(propertyPatch.Key, out var originalValue);
                    var patchedValue = ApplyPatch(originalValue, propertyPatch.Value, schema?.GetProperty(propertyPatch.Key), patchConfiguration);
                    if (patchedValue != null) patched[propertyPatch.Key] = patchedValue;
                    else if (patched.ContainsKey(propertyPatch.Key)) patched.Remove(propertyPatch.Key);
                }
                break;
        }
        return patched;
    }

    /// <summary>
    /// Patches the specified <see cref="JsonArray"/>
    /// </summary>
    /// <param name="target">The <see cref="JsonArray"/> to patch</param>
    /// <param name="patch">The patch to apply</param>
    /// <param name="schema">The <see cref="JsonArray"/> that describes the object to patch</param>
    /// <param name="patchConfiguration">An object used to configure the <see cref="JsonStrategicMergePatch"/></param>
    /// <returns>The patched <see cref="JsonArray"/></returns>
    public static JsonArray? ApplyPatch(JsonArray? target, JsonArray? patch, JsonSchema? schema = null, Configuration? patchConfiguration = null)
    {
        if (patch == null) return target;
        if (patchConfiguration == null) try { patchConfiguration = JsonSerializer.Deserialize<Configuration>(patch); } catch { }
        var patched = target.Copy()?.AsArray();
        patched ??= new();
        switch (patchConfiguration?.Strategy)
        {
            case Strategies.Delete: return null;
            case Strategies.Replace:
            case Strategies.Merge:
            default:
                if (patchConfiguration?.Strategy == Strategies.Replace) patched.Clear();
                foreach (var itemPatch in patch)
                {
                    if (itemPatch is JsonObject objectPatch)
                    {
                        patchConfiguration = JsonSerializer.Deserialize<Configuration>(objectPatch);
                        switch (patchConfiguration?.Strategy)
                        {
                            case Strategies.Delete:
                                var keyMetadata = patchConfiguration.Extensions?.FirstOrDefault();
                                if (!keyMetadata.HasValue) continue;
                                var toRemove = patched.OfType<JsonObject>().FirstOrDefault(p => p.TryGetPropertyValue(keyMetadata.Value.Key, out var value) && value?.ToString() == keyMetadata.Value.Value.ToString());
                                if (toRemove == null) continue;
                                patched.Remove(toRemove);
                                break;
                            case Strategies.Merge:
                            case Strategies.Replace:
                            default:
                                IEnumerable<JsonObject>? itemsToPatch = null;
                                var mergeKey = patchConfiguration?.MergeKey;
                                if (string.IsNullOrWhiteSpace(mergeKey)) mergeKey = schema?.GetPatchMergeKey();
                                if (!string.IsNullOrWhiteSpace(mergeKey) && patchConfiguration?.Extensions?.TryGetValue(mergeKey, out var patchValue) == true)
                                    itemsToPatch = patched.OfType<JsonObject>().Where(p => p.TryGetPropertyValue(mergeKey, out var originalValue) && originalValue?.ToString() == patchValue?.ToString()).ToList();
                                if (itemsToPatch?.Any() == false)
                                {
                                    var itemToPatch = objectPatch.Copy()?.AsObject();
                                    if (itemToPatch != null) itemsToPatch = new JsonObject[] { itemToPatch };
                                }
                                if (itemsToPatch != null)
                                {
                                    if (patchConfiguration?.Strategy == Strategies.Replace)
                                    {
                                        foreach (var itemToPatch in itemsToPatch)
                                        {
                                            foreach (var propertyKey in itemToPatch.Select(p => p.Key).ToList())
                                            {
                                                if (patchConfiguration?.RetainKeys?.Contains(propertyKey) == true) continue;
                                                itemToPatch.Remove(propertyKey);
                                            }
                                        }
                                    }
                                    foreach (var itemToPatch in itemsToPatch)
                                    {
                                        var itemIndex = patched.IndexOf(itemToPatch);
                                        if (itemIndex < 0)
                                        {
                                            patched.Add(itemToPatch);
                                            continue;
                                        }
                                        var patchedItem = ApplyPatch(itemToPatch, objectPatch, schema, patchConfiguration);
                                        patched.Remove(itemToPatch);
                                        patched.Insert(itemIndex, patchedItem);
                                    }
                                }
                                break;
                        }
                    }
                    else
                    {
                        patched.Add(itemPatch.Copy());
                    }
                }
                break;
        }
        return patched;
    }

    /// <summary>
    /// Represents an object used to configure a <see cref="JsonStrategicMergePatch"/>
    /// </summary>
    [DataContract]
    public class Configuration
    {

        /// <summary>
        /// Gets/sets the patch strategy to use
        /// </summary>
        [DataMember(Name = "$patch", Order = 1), JsonPropertyName("$patch")]
        public virtual string? Strategy { get; set; }

        /// <summary>
        /// Gets/sets a list containing the keys to retain when using the 'replace' strategy
        /// </summary>
        [DataMember(Name = "$retainKeys", Order = 2), JsonPropertyName("$retainKeys")]
        public virtual List<string>? RetainKeys { get; set; }

        /// <summary>
        /// Gets/sets the key to use when merging, replacing or deleting patched objects
        /// </summary>
        [DataMember(Name = "$mergeKey", Order = 3), JsonPropertyName("$mergeKey")]
        public virtual string? MergeKey { get; set; }

        /// <summary>
        /// Gets/sets an <see cref="IDictionary{TKey, TValue}"/> containing configuration extensions, if any
        /// </summary>
        [DataMember(Name = "extensions", Order = 4), JsonExtensionData]
        public virtual IDictionary<string, object>? Extensions { get; set; }

    }

    /// <summary>
    /// Enumerates all supported <see cref="JsonStrategicMergePatch"/> strategies
    /// </summary>
    public static class Strategies
    {
        /// <summary>
        /// Specifies the merge strategy
        /// </summary>
        public const string Merge = "merge";
        /// <summary>
        /// Specifies the merge strategy
        /// </summary>
        public const string Replace = "replace";
        /// <summary>
        /// Specifies the delete strategy
        /// </summary>
        public const string Delete = "delete";

        /// <summary>
        /// Gets all supported <see cref="JsonStrategicMergePatch"/> strategies
        /// </summary>
        /// <returns>A new <see cref="IEnumerable{T}"/> containing all supported <see cref="JsonStrategicMergePatch"/> strategies</returns>
        public static IEnumerable<string> AsEnumerable() 
        {
            yield return Merge;
            yield return Replace;
            yield return Delete;
        }

    }

    /// <summary>
    /// Enumerates all supported <see cref="JsonStrategicMergePatch"/> directives
    /// </summary>
    public static class Directives
    {
        /// <summary>
        /// Indicates the directive used to specify the patch strategy
        /// </summary>
        public const string Patch = "$patch";
        /// <summary>
        /// Indicates the directive used to retain a replaced object's keys
        /// </summary>
        public const string RetainKeys = "$retainKeys";
        /// <summary>
        /// Indicates the directive used to specify the key to identify the object(s) to patch
        /// </summary>
        public const string MergeKey = "$mergeKey";

        /// <summary>
        /// Gets all supported <see cref="JsonStrategicMergePatch"/> directives
        /// </summary>
        /// <returns>A new <see cref="IEnumerable{T}"/> containing all supported <see cref="JsonStrategicMergePatch"/> directives</returns>
        public static IEnumerable<string> AsEnumerable()
        {
            yield return Patch;
            yield return RetainKeys;
            yield return MergeKey;
        }

    }

    /// <summary>
    /// Enumerates all <see cref="JsonStrategicMergePatch"/>-specific <see cref="JsonSchema"/> properties
    /// </summary>
    public static class JsonSchemaProperties
    {

        /// <summary>
        /// Gets the prefix of all <see cref="JsonStrategicMergePatch"/>-specific <see cref="JsonSchema"/> properties
        /// </summary>
        public const string Prefix = "x-strategic-merge-patch-";
        /// <summary>
        /// Gets the name of the <see cref="JsonSchema"/> property used to specify an object's merge key
        /// </summary>
        public const string MergeKey = Prefix + "key";

    }

}
