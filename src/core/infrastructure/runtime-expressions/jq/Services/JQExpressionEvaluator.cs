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

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the JQ implementation of the <see cref="IExpressionEvaluator"/> interface
/// </summary>
public class JQExpressionEvaluator
    : IExpressionEvaluator
{

    /// <inheritdoc/>
    public object? Evaluate(string expression, object input, IDictionary<string, object>? arguments = null, Type? expectedType = null)
    {
        if(string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        if(input == null) throw new ArgumentNullException(nameof(input));
        if (expectedType == null) expectedType = typeof(object);

        expression = expression.Trim();
        if (expression.StartsWith("${")) expression = expression[2..^1].Trim();
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));

        var startInfo = new ProcessStartInfo()
        {
            FileName = "jq",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(expression);
        if(arguments != null)
        {
            foreach (var kvp in arguments.ToDictionary(a => a.Key, a => Serializer.Json.Serialize(a.Value)))
            {
                startInfo.ArgumentList.Add("--argjson");
                startInfo.ArgumentList.Add(kvp.Key);
                startInfo.ArgumentList.Add(kvp.Value);
            }
        }
        var files = new List<string>();
        var maxLength = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 8000 : 32699;
        if (startInfo.ArgumentList.Any(a => a.Length >= maxLength))
        {
            startInfo.ArgumentList.Clear();
            var filterFile = Path.GetTempFileName();
            File.WriteAllText(filterFile, expression);
            files.Add(filterFile);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(filterFile);
            if (arguments?.Any() == true)
            {
                foreach (var kvp in arguments)
                {
                    var argFile = Path.GetTempFileName();
                    File.WriteAllText(argFile, Serializer.Json.Serialize(kvp.Value));
                    files.Add(argFile);
                    startInfo.ArgumentList.Add("--argfile");
                    startInfo.ArgumentList.Add(kvp.Key);
                    startInfo.ArgumentList.Add(argFile);
                }
            }
        }
        startInfo.ArgumentList.Add("-c");

        using var process = new Process() { StartInfo = startInfo };
        process.Start();
        process.StandardInput.Write(Serializer.Json.Serialize(input));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        foreach (var file in files)
        {
            try { File.Delete(file); } catch { }
        }

        if (process.ExitCode != 0) throw new Exception($"An error occured while evaluting the specified expression: {error}");
        if (string.IsNullOrWhiteSpace(output)) return null;
        return Serializer.Json.Deserialize(output, expectedType);
    }

}
