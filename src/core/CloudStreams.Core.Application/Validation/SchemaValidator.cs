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

using CloudStreams.Core.Application.Services;
using FluentValidation.Results;
using Json.Schema;
using Neuroglia.Serialization;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Application.Validation;

/// <summary>
/// Validates <see cref="Schema"/>s, and the changes made to them: versions can be added, but neither changed nor removed, and each version must be compatible with the
/// previous one as the schema requires (see #213)
/// </summary>
/// <param name="serializer">The service used to read the JSON schemas of versions</param>
public class SchemaValidator(IJsonSerializer serializer)
{

    /// <summary>
    /// Validates the specified schema
    /// </summary>
    /// <param name="schema">The schema to validate</param>
    /// <param name="original">The schema before it was changed, if it is being changed</param>
    /// <returns>The result of the validation</returns>
    public virtual ValidationResult Validate(Schema schema, Schema? original = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var failures = new List<ValidationFailure>();
        var compatibility = schema.Spec.GetCompatibility();
        if (!SchemaCompatibility.AsEnumerable().Contains(compatibility)) failures.Add(new("spec.compatibility", $"'spec.compatibility' must be one of: {string.Join(", ", SchemaCompatibility.AsEnumerable())}"));
        var versions = schema.Spec.Versions ?? [];
        if (versions.Count == 0) failures.Add(new("spec.versions", "'spec.versions' must contain at least one version"));
        var documents = new List<JsonNode?>(versions.Count);
        for (var i = 0; i < versions.Count; i++)
        {
            var version = versions[i];
            if (version.Version < 1) failures.Add(new($"spec.versions[{i}].version", "Versions must be positive"));
            if (i > 0 && version.Version <= versions[i - 1].Version) failures.Add(new($"spec.versions[{i}].version", $"Versions must increase: version {version.Version} follows version {versions[i - 1].Version}"));
            var document = version.Schema == null ? null : serializer.SerializeToNode(version.Schema);
            documents.Add(document);
            if (document is not JsonObject && !(document is JsonValue value && value.TryGetValue<bool>(out _))) failures.Add(new($"spec.versions[{i}].schema", $"The schema of version {version.Version} must be a JSON schema"));
            else
            {
                try { JsonSchema.FromText(document.ToJsonString()); }
                catch (Exception ex) when (ex is System.Text.Json.JsonException or ArgumentException or InvalidOperationException) { failures.Add(new($"spec.versions[{i}].schema", $"The schema of version {version.Version} is not a valid JSON schema: {ex.Message}")); }
            }
        }
        if (failures.Count > 0) return new(failures);
        // the versions that were already accepted are kept as they are, and only the new ones are checked for compatibility
        if (original != null)
        {
            foreach (var existing in original.Spec.Versions)
            {
                var index = versions.FindIndex(v => v.Version == existing.Version);
                if (index < 0) failures.Add(new("spec.versions", $"Version {existing.Version} cannot be removed"));
                else if (!JsonNode.DeepEquals(serializer.SerializeToNode(existing.Schema), documents[index])) failures.Add(new($"spec.versions[{index}].schema", $"Version {existing.Version} cannot be changed: add a new version instead"));
            }
        }
        var lastAccepted = original?.Spec.Versions.LastOrDefault()?.Version;
        for (var i = 1; i < versions.Count; i++)
        {
            if (versions[i].Version <= lastAccepted) continue;
            foreach (var reason in SchemaCompatibilityChecker.Check(documents[i - 1], documents[i], compatibility))
                failures.Add(new($"spec.versions[{i}].schema", $"Version {versions[i].Version} is not {compatibility} compatible with version {versions[i - 1].Version}: {reason}"));
        }
        return new(failures);
    }

}
