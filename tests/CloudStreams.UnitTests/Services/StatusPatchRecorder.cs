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
/// Records the status patches a <see cref="SubscriptionHandler"/> applies, and exposes them as flat operations so tests can assert on paths and values
/// </summary>
/// <remarks>
/// The handler only ever patches through <c>PatchStatusAsync</c>, so every recorded patch is a status patch.
/// </remarks>
public class StatusPatchRecorder
{

    readonly List<Patch> _patches = [];
    readonly Lock _lock = new();

    /// <summary>
    /// Gets the patches recorded so far, in order
    /// </summary>
    public IReadOnlyList<Patch> Patches { get { lock (this._lock) return [.. this._patches]; } }

    /// <summary>
    /// Gets the amount of patches recorded so far
    /// </summary>
    public int Count { get { lock (this._lock) return this._patches.Count; } }

    /// <summary>
    /// Records the specified patch
    /// </summary>
    public void Record(Patch patch) { lock (this._lock) this._patches.Add(patch); }

    /// <summary>
    /// Clears everything recorded so far
    /// </summary>
    public void Clear() { lock (this._lock) this._patches.Clear(); }

    /// <summary>
    /// Gets every operation of every recorded patch, in order
    /// </summary>
    public IReadOnlyList<PatchOperation> Operations => [.. this.Patches.SelectMany(GetOperations)];

    /// <summary>
    /// Determines whether or not any recorded operation targets the specified path
    /// </summary>
    /// <param name="path">The JSON pointer to look for, for example <c>/status/stream/ackedOffset</c></param>
    public bool Touched(string path) => this.Operations.Any(o => o.Path.ToString() == path);

    /// <summary>
    /// Gets the non-null values written to the specified path, in order
    /// </summary>
    /// <param name="path">The JSON pointer to look for, for example <c>/status/stream/ackedOffset</c></param>
    /// <remarks>
    /// Removals carry no value, and <c>CommitOffsetAsync(null)</c> emits exactly that for the acked offset, so they are filtered out rather than
    /// left to blow up in a caller's projection.
    /// </remarks>
    public IReadOnlyList<JsonNode> ValuesWrittenTo(string path) => [.. this.Operations.Where(o => o.Path.ToString() == path).Select(o => o.Value).OfType<JsonNode>()];

    /// <summary>
    /// Gets the offsets written to the specified path, in order
    /// </summary>
    /// <param name="path">The JSON pointer to look for. Defaults to the acked offset</param>
    /// <remarks>Read as <see cref="ulong"/>, matching <see cref="CloudEventStreamStatus.AckedOffset"/>, so an offset past <see cref="int.MaxValue"/> does not overflow.</remarks>
    public IReadOnlyList<ulong> OffsetsWrittenTo(string path = "/status/stream/ackedOffset") => [.. this.ValuesWrittenTo(path).Select(v => v.GetValue<ulong>())];

    /// <summary>
    /// Renders every recorded operation, so a failing assertion can say what was actually patched
    /// </summary>
    public string Describe()
    {
        var operations = this.Operations;
        return operations.Count == 0 ? "(no operations recorded)" : string.Join(Environment.NewLine, operations.Select(o => $"{o.Op} {o.Path} {o.Value?.ToJsonString()}"));
    }

    static IEnumerable<PatchOperation> GetOperations(Patch patch) => patch.Document switch
    {
        JsonPatch jsonPatch => jsonPatch.Operations,
        _ => []
    };

}
