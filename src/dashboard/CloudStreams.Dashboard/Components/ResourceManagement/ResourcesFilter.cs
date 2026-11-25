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

namespace CloudStreams.Dashboard.Components.ResourceManagement;

/// <summary>
/// Holds filters used to request resources
/// </summary>
public record ResourcesFilter
{

    /// <summary>
    /// Gets/sets a list that contains the label selectors, if any, used to filter the resources to list
    /// </summary>
    public EquatableList<LabelSelector>? LabelSelectors { get; set; }

}

/// <summary>
/// Describes what became of the documents of an import
/// </summary>
public sealed class ResourceImportResult
{

    /// <summary>
    /// Gets the names of the resources that have been created
    /// </summary>
    public List<string> Created { get; } = [];

    /// <summary>
    /// Gets the names of the resources that have been updated
    /// </summary>
    public List<string> Updated { get; } = [];

    /// <summary>
    /// Gets the names of the resources that already were as imported
    /// </summary>
    public List<string> Unchanged { get; } = [];

    /// <summary>
    /// Gets the descriptions of the documents that could not be imported, and why
    /// </summary>
    public List<string> Failed { get; } = [];

    /// <summary>
    /// Gets a summary of the import
    /// </summary>
    /// <returns>A summary of the import, e.g. "2 created, 1 updated"</returns>
    public override string ToString() => string.Join(", ", new[]
    {
        Created.Count > 0 ? $"{Created.Count} created" : null,
        Updated.Count > 0 ? $"{Updated.Count} updated" : null,
        Unchanged.Count > 0 ? $"{Unchanged.Count} unchanged" : null,
        Failed.Count > 0 ? $"{Failed.Count} failed" : null
    }.Where(s => s != null)) is { Length: > 0 } summary ? summary : "nothing to import";

}
