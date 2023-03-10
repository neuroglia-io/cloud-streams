/**
 * Adds a validation schema to monaco editor's diagnostics options
 * @param {any} language The targeted language
 * @param {any} schema The validation schema to add
 * @param {any} schemaUri The schema identifier
 * @param {any} schemaType The schema type, used to match the "file"/model URI
 * @returns
 */
export function addValidationSchema(language, schema, schemaUri, schemaType) {
    if (language === 'json') {
        const diagnosticsOptions = monaco.languages.json.jsonDefaults.diagnosticsOptions;
        if (!!diagnosticsOptions.schemas.find(s => s.uri === schemaUri)) {
            return;
        }
        monaco.languages.json.jsonDefaults.setDiagnosticsOptions({
            validate: true,
            allowComments: false,
            enableSchemaRequest: true,
            schemas: [
                ...diagnosticsOptions.schemas,
                {
                    schema: JSON.parse(schema),
                    uri: schemaUri,
                    fileMatch: [schemaType]
                }
            ]
        });
    }
}