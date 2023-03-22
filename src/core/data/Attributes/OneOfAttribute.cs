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

namespace CloudStreams.Core.Data.Attributes;

/// <summary>
/// Represents an <see cref="Attribute"/> used to validate that the value of the marked property falls within range
/// </summary>
/// <typeparam name="T">The type of value to support</typeparam>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class OneOfAttribute<T>
    : ValidationAttribute
{

    /// <summary>
    /// Initializes a new <see cref="OneOfAttribute{T}"/>
    /// </summary>
    /// <param name="supportedValues">A <see cref="List{T}"/> containing all supported values</param>
    public OneOfAttribute(params T[] supportedValues) 
    {
        this.SupportedValues = supportedValues.ToList();
    }

    /// <summary>
    /// Gets a <see cref="List{T}"/> containing all supported values
    /// </summary>
    protected List<T> SupportedValues { get; }

    /// <inheritdoc/>
    public override bool IsValid(object? value)
    {
        if (value == null) return true;
        var result =  value is T t && this.SupportedValues.Contains(t);
        return result;
    }

}
