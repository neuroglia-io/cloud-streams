﻿namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to reference a cloud event partition
/// </summary>
[DataContract]
public record PartitionReference
{

    /// <summary>
    /// Initializes a new <see cref="PartitionReference"/>
    /// </summary>
    public PartitionReference() { }

    /// <summary>
    /// Initializes a new <see cref="PartitionReference"/>
    /// </summary>
    /// <param name="type">The referenced stream partition's type</param>
    /// <param name="id">The referenced stream partition's id</param>
    public PartitionReference(CloudEventPartitionType type, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
        this.Type = type;
        this.Id = id;
    }

    /// <summary>
    /// Gets/sets the referenced stream partition's type
    /// </summary>
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual CloudEventPartitionType Type { get; set; }

    /// <summary>
    /// Gets/sets the referenced stream partition's id
    /// </summary>
    [DataMember(Order = 2, Name = "id"), JsonPropertyName("id"), YamlMember(Alias = "id")]
    public virtual string Id { get; set; } = null!;

}