namespace CloudStreams.Dashboard.Components.ReadOptionsFormStateManagement;

/// <summary>
/// Represents the state of the form used to manipulate <see cref="StreamReadOptions"/>
/// </summary>
public record ReadOptionsFormState
{
    /// <summary>
    /// Gets/sets a reference to the cloud event partition's type to read, if any
    /// </summary>
    public CloudEventPartitionType? PartitionType { get; set; } = null;
    /// <summary>
    /// Gets/sets a reference to the cloud event partition's id to read, if any
    /// </summary>
    public string? PartitionId { get; set; } = null;

    /// <summary>
    /// Gets/sets the direction in which to read the stream of cloud events
    /// </summary>
    public StreamReadDirection Direction { get; set; } = StreamReadDirection.Backwards;

    /// <summary>
    /// Gets/sets the offset starting from which to read the stream
    /// </summary>
    public long? Offset { get; set; } = null;

    /// <summary>
    /// Gets/sets the amount of events to read from the stream
    /// </summary>
    public ulong? Length { get; set; } = null;

    /// <summary>
    /// Gets/sets the total amount of events in the stream
    /// </summary>
    public ulong? MaxLength { get; set; } = null;

    /// <summary>
    /// Gets the <see cref="List{T}"/> of suggested <see cref="PartitionReference"/>s
    /// </summary>
    public List<string>? Partitions { get; set; } = new();
}
