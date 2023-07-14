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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace CloudStreams.Core.Serialization;

/// <summary>
/// Represents an <see cref="EnumConverter"/> used to convert enum using the values specified by <see cref="EnumMemberAttribute"/>s
/// </summary>
public class StringEnumMemberConverter
    : EnumConverter
{

    /// <inheritdoc/>
    public StringEnumMemberConverter([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields)] Type type) : base(type) { }

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string strValue)
        {
            try
            {
                foreach (var name in Enum.GetNames(EnumType))
                {
                    var field = EnumType.GetField(name);
                    if (field != null)
                    {
                        var enumMember = (EnumMemberAttribute)(field.GetCustomAttributes(typeof(EnumMemberAttribute), true).Single());
                        if (strValue.Equals(enumMember.Value, StringComparison.OrdinalIgnoreCase)) return Enum.Parse(EnumType, name, true);
                    }
                }
            }
            catch (Exception e)
            {
                throw new FormatException((string)value, e);
            }
        }
        return base.ConvertFrom(context, culture, value);
    }

}