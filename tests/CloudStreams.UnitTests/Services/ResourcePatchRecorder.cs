// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Json.Patch;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// Represents a single JSON Patch operation written by the handler
/// </summary>
/// <param name="Operation">The operation performed, for example <c>add</c> or <c>replace</c></param>
/// <param name="Path">The JSON Pointer the operation targets, for example <c>/status/subscriber/state</c></param>
/// <param name="Value">The value the operation writes, if any</param>
public record RecordedPatchOperation(string Operation, string Path, JsonNode? Value)
{

    /// <inheritdoc/>
    public override string ToString() => $"{this.Operation} {this.Path} = {this.Value?.ToJsonString()}";

}

/// <summary>
/// Records the status patches a <see cref="Broker.Application.Services.SubscriptionHandler"/> writes
/// </summary>
/// <remarks>
/// <para>
/// The handler writes status through the <c>PatchStatusAsync&lt;TResource&gt;</c> extension method, which no mocking library can
/// intercept. It delegates to <c>PatchSubResourceAsync</c>, which is a real interface member, so that is what is recorded here.
/// </para>
/// <para>
/// Patches are decoded rather than applied: the handler discards every patch result and never re-reads its own writes, so
/// applying them would add moving parts without changing what any assertion can observe. Because the patches are built by
/// diffing, the same logical write lands at a different pointer depending on which parent objects already exist — a first
/// write produces <c>add /status</c> carrying a whole object, a later one <c>replace /status/subscriber/state</c>.
/// <see cref="LastValueAt"/> therefore resolves a pointer *into* each operation's value, so assertions can name the property
/// they care about without depending on how the diff happened to be shaped.
/// </para>
/// </remarks>
public class ResourcePatchRecorder
{

    readonly Lock _lock = new();
    readonly List<RecordedPatchOperation> _operations = [];
    int _patchCount;

    /// <summary>
    /// Initializes a new <see cref="ResourcePatchRecorder"/>
    /// </summary>
    /// <param name="subscription">The <see cref="Subscription"/> the recorded patches apply to</param>
    public ResourcePatchRecorder(Subscription subscription)
    {
        this.Current = subscription;
        this.Repository = Substitute.For<IResourceRepository>();
        this.Repository.PatchSubResourceAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                this.Record(call.Arg<Patch>());
                return Task.FromResult<IResource>(this.Current);
            });
        this.Repository.PatchAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                this.Record(call.Arg<Patch>());
                return Task.FromResult<IResource>(this.Current);
            });
        this.Repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IResource?>(this.Current));
    }

    /// <summary>
    /// Gets the substituted <see cref="IResourceRepository"/> to hand to the handler
    /// </summary>
    public IResourceRepository Repository { get; }

    /// <summary>
    /// Gets the <see cref="Subscription"/> as it stands after every recorded patch has been applied.
    /// <para>
    /// Patches are applied, not just recorded, so that an implementation which dedups by re-reading the resource observes its
    /// own writes. Without this the fixture would only ever admit one particular deduplication strategy.
    /// </para>
    /// </summary>
    public Subscription Current { get; private set; }

    /// <summary>
    /// Gets a list containing every operation the handler patched, across all patches, in order
    /// </summary>
    public IReadOnlyList<RecordedPatchOperation> Operations => this._operations;

    /// <summary>
    /// Gets the number of patch requests the handler issued. A single patch may carry more than one operation
    /// </summary>
    public int PatchCount => this._patchCount;

    /// <summary>
    /// Gets every value written to the specified JSON Pointer, in order
    /// </summary>
    /// <param name="path">The JSON Pointer to read, for example <c>/status/subscriber/state</c></param>
    /// <returns>The values written to the specified path, in order</returns>
    public IReadOnlyList<string> ValuesAt(string path)
    {
        lock (this._lock) return [.. this.EffectiveWritesAt(path).Where(v => v != null).Select(v => v!)];
    }

    /// <summary>
    /// Gets the value last written to the specified JSON Pointer, if any
    /// </summary>
    /// <remarks>
    /// Returns null both when the path was never written and when it was written and later cleared, because a diff-derived
    /// patch clears a field with a <c>remove</c> or a null-valued write. Both mean "no value stands at this path".
    /// </remarks>
    /// <param name="path">The JSON Pointer to read, for example <c>/status/subscriber/state</c></param>
    /// <returns>The value standing at the specified path, or null if none does</returns>
    public string? LastValueAt(string path)
    {
        lock (this._lock) return this.EffectiveWritesAt(path).LastOrDefault();
    }

    /// <summary>
    /// Counts the writes of a value to the specified JSON Pointer, ignoring clears
    /// </summary>
    /// <param name="path">The JSON Pointer to count writes to, for example <c>/status/subscriber/state</c></param>
    /// <returns>The number of operations that wrote a value to the specified path</returns>
    public int WriteCountAt(string path) => this.ValuesAt(path).Count;

    /// <summary>
    /// Walks the recorded operations, yielding the value each one leaves standing at the specified path. A null entry marks a clear
    /// </summary>
    IEnumerable<string?> EffectiveWritesAt(string path)
    {
        foreach (var operation in this._operations)
        {
            var targetsPath = operation.Path.Equals(path, StringComparison.OrdinalIgnoreCase);
            var containsPath = path.StartsWith($"{operation.Path}/", StringComparison.OrdinalIgnoreCase);
            if (!targetsPath && !containsPath) continue;
            if (operation.Operation == "remove") { if (targetsPath || containsPath) yield return null; continue; }
            if (operation.Operation is not ("add" or "replace")) continue;
            var value = targetsPath ? operation.Value : Resolve(operation.Value, path[(operation.Path.Length + 1)..]);
            //an object written over the path without the property, or an explicit null, both clear whatever stood there
            if (targetsPath || value != null || operation.Value is JsonObject) yield return value == null || value.GetValueKind() == System.Text.Json.JsonValueKind.Null ? null : value.ToString();
        }
    }

    static JsonNode? Resolve(JsonNode? node, string pointer)
    {
        foreach (var segment in pointer.Split('/'))
        {
            if (node is not JsonObject o || !o.TryGetPropertyValue(segment, out node)) return null;
        }
        return node;
    }

    void Record(Patch patch)
    {
        lock (this._lock)
        {
            this._patchCount++;
            if (patch.Document is not JsonPatch document) return;
            foreach (var operation in document.Operations) this._operations.Add(new(operation.Op.ToString().ToLowerInvariant(), operation.Path.ToString(), operation.Value?.DeepClone()));
            this.Apply(document);
        }
    }

    /// <summary>
    /// Applies the specified <see cref="JsonPatch"/> to the tracked <see cref="Subscription"/>
    /// </summary>
    void Apply(JsonPatch document)
    {
        try
        {
            var node = Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToNode(this.Current);
            var result = document.Apply(node);
            if (!result.IsSuccess || result.Result == null) return;
            if (Neuroglia.Serialization.Json.JsonSerializer.Default.Deserialize<Subscription>(result.Result.ToJsonString()) is Subscription patched) this.Current = patched;
        }
        catch { /* a patch the fixture cannot round-trip still gets recorded; only the re-read view degrades */ }
    }

}
