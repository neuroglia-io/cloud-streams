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

using CloudStreams.Core;
using CloudStreams.Core.Data.Models;
using CloudStreams.Documentation.Markdown.Generator;
using System.Text;
using System.Text.Json.Serialization;

foreach (var type in TypeCacheUtil.FindFilteredTypes("csdg:resources", t => t.IsClass && !t.IsInterface && !t.IsAbstract && !t.IsGenericTypeDefinition && t.IsPublic && t.Namespace == typeof(Subscription).Namespace, typeof(Broker).Assembly))
{
    GenerateMarkdownDocumentationFor(type);
}

void GenerateMarkdownDocumentationFor(Type type)
{
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine(
$"""
## {type.Name}

### Description

{ConvertLineBreaksToMarkdown(XmlDocumentationHelper.SummaryOf(type))}

```c#
public class {type.Name}
```

### Properties

{GeneratePropertiesMarkdownTableFor(type)}

### Examples

{GenerateExamplesFor(type)}

""");

    File.WriteAllText($"{type.Name}.md", stringBuilder.ToString());
}

string GeneratePropertiesMarkdownTableFor(Type type)
{
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine(
$"""
| Name | Type | Required | Description |
|------|:----:|:--------:|-------------|
""");
    foreach(var property in type.GetProperties().Where(p => p.CanRead && p.CanWrite && !p.TryGetCustomAttribute<JsonIgnoreAttribute>(out _)))
    {
        stringBuilder.AppendLine(
$"""
| {property.Name.ToCamelCase()} | {GenerateTypeReferenceFor(property.PropertyType)} | {(property.TryGetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>(out _) ? "`yes`" : "`no`")} | {ConvertLineBreaksToMarkdown(XmlDocumentationHelper.SummaryOf(property))} |
""");
    }
    return stringBuilder.ToString();
}

string GenerateTypeReferenceFor(Type type)
{
    return 
$"""
[{type.Name}](/{type.Name}.md)
""";
}

string GenerateExamplesFor(Type type)
{
    var sample = type.GetSample();
    return
$$"""

<table>
<tr>
<th>JSON</th>
<th>YAML</th>
</tr>
<tr>
<td valign="top">

```json

{{Serializer.Json.Serialize(sample, true)}}

```

</td>
<td valign="top">

```yaml

{{Serializer.Yaml.Serialize(sample)}}

```

</td>
</tr>
</table>

""";
}

string? ConvertLineBreaksToMarkdown(string? input)
{
    return input?
        .Replace(Environment.NewLine, "<br>")
        .Replace("\r\n", "<br>")
        .Replace("\n", "<br>")
        .Replace("\t", "<br>")
        .Replace("<para/>", "<br>")
        .Replace("<para />", "<br>")
        .Replace("<para></para>", "<br>");
}