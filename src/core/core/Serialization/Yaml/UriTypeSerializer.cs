﻿// Copyright © 2023-Present The Cloud Streams Authors
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

using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace CloudStreams.Core;

/// <summary>
/// Represents the <see cref="IYamlTypeConverter"/> used to serialize <see cref="Uri"/>s
/// </summary>
public class UriTypeSerializer
    : IYamlTypeConverter
{

    /// <inheritdoc/>
    public virtual bool Accepts(Type type) => typeof(Uri).IsAssignableFrom(type);

    /// <inheritdoc/>
    public virtual object ReadYaml(IParser parser, Type type)
    {
        Scalar scalar = (Scalar)parser.Current!;
        parser.MoveNext();
        return new Uri(scalar.Value, UriKind.RelativeOrAbsolute);
    }

    /// <inheritdoc/>
    public virtual void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value == null) return;
        emitter.Emit(new Scalar(((Uri)value).ToString()));
    }

}