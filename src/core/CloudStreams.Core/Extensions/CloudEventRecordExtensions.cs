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

using CloudStreams.Core.Data;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="CloudEventRecord"/>s
/// </summary>
public static class CloudEventRecordExtensions
{

    /// <summary>
    /// Converts the <see cref="CloudEventRecord"/> into the <see cref="CloudEvent"/> it describes
    /// </summary>
    /// <param name="record">The <see cref="CloudEventRecord"/> to convert</param>
    /// <returns>The <see cref="CloudEvent"/> described by the converted <see cref="CloudEventRecord"/></returns>
    public static CloudEvent ToCloudEvent(this CloudEventRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        var e = ((CloudEventDescriptor)record).ToCloudEvent();
        if (e.ExtensionAttributes == null) e.ExtensionAttributes = new Dictionary<string, object>();
        e.ExtensionAttributes.Add(CloudEventExtensionAttributes.Sequence, record.Sequence);
        return e;
    }

}