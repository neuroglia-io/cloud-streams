namespace CloudStreams.Core;

/// <summary>
/// Provides functionality to serialize/deserialize objects to/from JSON and YAML
/// </summary>
public static partial class Serializer
{

    /// <summary>
    /// Provides functionality to serialize/deserialize objects to/from JSON
    /// </summary>
    public static class Json
    {

        /// <summary>
        /// Gets/sets an <see cref="Action{T}"/> used to configure the <see cref="JsonSerializerOptions"/> used by default
        /// </summary>
        public static Action<JsonSerializerOptions>? DefaultOptionsConfiguration { get; set; } = (options) =>
        {
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
        };

        static JsonSerializerOptions? _DefaultOptions;
        /// <summary>
        /// Gets/sets the default <see cref="JsonSerializerOptions"/>
        /// </summary>
        public static JsonSerializerOptions DefaultOptions
        {
            get
            {
                if (_DefaultOptions != null) return _DefaultOptions;
                _DefaultOptions = new JsonSerializerOptions();
                DefaultOptionsConfiguration?.Invoke(_DefaultOptions);
                return _DefaultOptions;
            }
        }

        /// <summary>
        /// Serializes the specified object to JSON
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="graph">The object to serialized</param>
        /// <returns>The JSON of the serialized object</returns>
        public static string Serialize<T>(T graph) => JsonSerializer.Serialize(graph, DefaultOptions);

        /// <summary>
        /// Serializes the specified object into a new <see cref="JsonNode"/>
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="graph">The object to serialize</param>
        /// <returns>A new <see cref="JsonNode"/></returns>
        public static JsonNode? SerializeToNode<T>(T graph) => JsonSerializer.SerializeToNode(graph, DefaultOptions);

        /// <summary>
        /// Serializes the specified object into a new <see cref="JsonElement"/>
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="graph">The object to serialize</param>
        /// <returns>A new <see cref="JsonElement"/></returns>
        public static JsonElement? SerializeToElement<T>(T graph) => JsonSerializer.SerializeToElement(graph, DefaultOptions);

        /// <summary>
        /// Deserializes the specified JSON input
        /// </summary>
        /// <param name="json">The JSON input to deserialize</param>
        /// <param name="returnType">The type to deserialize the JSON into</param>
        /// <returns>An object that results from the specified JSON input's deserialization</returns>
        public static object? Deserialize(string json, Type returnType) => JsonSerializer.Deserialize(json, returnType); 

        /// <summary>
        /// Deserializes the specified <see cref="JsonElement"/>
        /// </summary>
        /// <typeparam name="T">The type to deserialize the specified <see cref="JsonElement"/> into</typeparam>
        /// <param name="element">The <see cref="JsonElement"/> to deserialize</param>
        /// <returns>The deserialized value</returns>
        public static T? Deserialize<T>(JsonElement element) => JsonSerializer.Deserialize<T>(element, DefaultOptions);

        /// <summary>
        /// Deserializes the specified JSON input
        /// </summary>
        /// <typeparam name="T">The type to deserialize the specified JSON into</typeparam>
        /// <param name="json">The JSON input to deserialize</param>
        /// <returns>The deserialized value</returns>
        public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, DefaultOptions);

        /// <summary>
        /// Deserializes the specified <see cref="JsonNode"/>
        /// </summary>
        /// <typeparam name="T">The type to deserialize the specified <see cref="JsonNode"/> into</typeparam>
        /// <param name="node">The <see cref="JsonNode"/> to deserialize</param>
        /// <returns>The deserialized value</returns>
        public static T? Deserialize<T>(JsonNode node) => JsonSerializer.Deserialize<T>(node, DefaultOptions);

        /// <summary>
        /// Deserializes the specified JSON input
        /// </summary>
        /// <typeparam name="T">The type to deserialize the specified JSON into</typeparam>
        /// <param name="buffer">The JSON input to deserialize</param>
        /// <returns>The deserialized value</returns>
        public static T? Deserialize<T>(ReadOnlySpan<byte> buffer) => JsonSerializer.Deserialize<T>(buffer, DefaultOptions);

        /// <summary>
        /// Deserializes the specified <see cref="Stream"/> as a new <see cref="IAsyncEnumerable{T}"/>
        /// </summary>
        /// <typeparam name="T">The expected type of elements to enumerate</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to deserialize</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
        /// <returns>A new <see cref="IAsyncEnumerable{T}"/></returns>
        public static IAsyncEnumerable<T?> DeserializeAsyncEnumerable<T>(Stream stream, CancellationToken cancellationToken = default) => JsonSerializer.DeserializeAsyncEnumerable<T>(stream, DefaultOptions, cancellationToken);

        /// <summary>
        /// Converts the specified YAML input into JSON
        /// </summary>
        /// <param name="yaml">The YAML input to convert</param>
        /// <returns>The YAML input converted into JSON</returns>
        public static string ConvertFromYaml(string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml)) return null!;
            var graph = Yaml.Deserialize<object>(yaml);
            return Serialize(graph);
        }

        /// <summary>
        /// Converts the specified JSON input into YAML
        /// </summary>
        /// <param name="json">The JSON input to convert</param>
        /// <returns>The JSON input converted into YAML</returns>
        public static string ConvertToYaml(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null!;
            var graph = Deserialize<object>(json);
            return Yaml.Serialize(graph);
        }

    }

}