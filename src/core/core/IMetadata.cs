// Copyright © 2023-Present The Cloud Streams Authors
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

namespace CloudStreams.Core;

/// <summary>
/// Defines the fundamentals of an object described by metadata
/// </summary>
public interface IMetadata
{

    /// <summary>
    /// Gets the metadata that describes the object
    /// </summary>
    object Metadata { get; }

}

/// <summary>
/// Defines the fundamentals of an object described by metadata
/// </summary>
/// <typeparam name="TMetadata">The type of the metadata</typeparam>
public interface IMetadata<TMetadata>
    : IMetadata
    where TMetadata : class, new()
{

    /// <summary>
    /// Gets the metadata that describes the object
    /// </summary>
    new TMetadata Metadata { get; }

}
