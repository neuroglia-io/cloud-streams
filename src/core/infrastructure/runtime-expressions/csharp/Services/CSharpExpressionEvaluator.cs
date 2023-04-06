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

using CloudStreams.Core.Data.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;
using System.Text;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the CSharp implementation of the <see cref="IExpressionEvaluator"/> interface
/// </summary>
public class CSharpExpressionEvaluator
    : IExpressionEvaluator
{

    /// <summary>
    /// The execution path of the current assembly
    /// </summary>
    private string ExecutionPath = Path.GetDirectoryName(typeof(object).Assembly.Location) + Path.DirectorySeparatorChar;

    /// <summary>
    /// Instanciates a new <see cref="CSharpExpressionEvaluator"/>
    /// </summary>
    public CSharpExpressionEvaluator()
    {
        this.AddReference("Microsoft.CSharp.dll");
        this.AddReference("System.Private.CoreLib.dll");
        this.AddReference("System.Runtime.dll");
        this.AddReference("System.Text.RegularExpressions.dll");
        this.AddReference("System.Linq.dll");
        this.AddReference("System.Linq.Expressions.dll");
    }

    /// <summary>
    /// Holds a list of referenced DLLs used by the evaluator
    /// </summary>
    private HashSet<PortableExecutableReference> References { get; } = new HashSet<PortableExecutableReference>();

    private void AddReference(string dll)
    {
        var file = Path.GetFullPath(this.ExecutionPath + dll);
        if (!File.Exists(file))
        {
            return;
        }
        if (References.Any(r => r.FilePath == file))
        {
            return;
        }
        var reference = MetadataReference.CreateFromFile(file);
        this.References.Add(reference);
    }

    /// <inheritdoc/>
    public object? Evaluate(string expression, object input, IDictionary<string, object>? arguments = null, Type? expectedType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (expectedType == null) expectedType = typeof(object);
        expression = expression.Trim();
        if (expression.StartsWith("${"))
            expression = expression[2..^1].Trim();
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));

        var code = $$"""
            using System.Collections;
            using System.Collections.Generic;
            using System;
            using System.Text;
            using System.Text.RegularExpressions;
            using System.Linq;

            namespace _ExpressionEvaluator {
                public class  _CSharpEvaluator {
                    public object? Evaluate(dynamic input, IDictionary<string, object>? arguments = null) {
                        {{(arguments == null ? "" : arguments.Aggregate(
                            new StringBuilder(), 
                            (builder, argument) => builder.AppendFormat("var {0} = arguments[\"{0}\"];\r\n", argument.Key)
                        ))}}
                        return {{expression}};
                    }
                }
            }
            """;
        var tree = SyntaxFactory.ParseSyntaxTree(code);
        var compilation = CSharpCompilation.Create("CSharpEvaluator.cs")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release))
                .WithReferences(References)
                .AddSyntaxTrees(tree);
        Assembly assembly;
        using Stream codeStream = new MemoryStream();
        EmitResult compilationResult = compilation.Emit(codeStream);
        if (!compilationResult.Success)
        {
            string errorMessage;
            var builder = new StringBuilder();
            foreach (var diag in compilationResult.Diagnostics)
            {
                builder.AppendLine(diag.ToString());
            }
            errorMessage = builder.ToString();
            return null;
        }
        assembly = Assembly.Load(((MemoryStream)codeStream).ToArray());
        if (assembly == null)
        {
            return null;
        }
        dynamic instance = assembly.CreateInstance("_ExpressionEvaluator._CSharpEvaluator")!;
        if (instance == null)
        {
            return null;
        }
        var result = instance.Evaluate(input, arguments);
        if (expectedType == typeof(object))
            return result;
        return Serializer.Json.Deserialize(Serializer.Json.Serialize(result), expectedType);
    }
}
