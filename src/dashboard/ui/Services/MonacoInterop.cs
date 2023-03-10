using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// The service used to build a bridge with the monaco interop extension
/// </summary>
public class MonacoInterop
    : IAsyncDisposable
{
    /// <summary>
    /// A reference to the js interop module
    /// </summary>
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;


    /// <summary>
    /// Constructs a new <see cref="MonacoInterop"/>
    /// </summary>
    /// <param name="jsRuntime">The service used to interop with JS</param>
    public MonacoInterop(IJSRuntime jsRuntime)
    {
        moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/monaco-editor-interop-extension.js").AsTask());
    }

    /// <summary>
    /// Adds the provided schema to monaco editor's diagnostics options
    /// </summary>
    /// <param name="language">The targeted language, JSON or YAML</param>
    /// <param name="schema">The JSON Schema used for validation</param>
    /// <param name="schemaUri">The schema identifier URI</param>
    /// <param name="schemaType">The schema type, used to match the "file"/model URI</param>
    /// <returns>A <see cref="ValueTask"/></returns>
    public async ValueTask AddValidationSchemaAsync(string language, string schema, string schemaUri, string schemaType)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("addValidationSchema", language, schema, schemaUri, schemaType);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
