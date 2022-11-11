using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Org.BouncyCastle.Asn1.X509;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Serialization;
using Umbraco.Extensions;

namespace Umbraco.Cms.Core.Manifest;

/// <summary>
///     Provides a json read converter for <see cref="IDataEditor" /> in manifests.
/// </summary>
internal class DataEditorConverter : JsonNetReadConverter<IDataEditor>
{
    private readonly IDataValueEditorFactory _dataValueEditorFactory;
    private readonly IIOHelper _ioHelper;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly ILocalizedTextService _textService;
    private const string SupportsReadOnly = "supportsReadOnly";

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataEditorConverter" /> class.
    /// </summary>
    public DataEditorConverter(
        IDataValueEditorFactory dataValueEditorFactory,
        IIOHelper ioHelper,
        ILocalizedTextService textService,
        IShortStringHelper shortStringHelper,
        IJsonSerializer jsonSerializer)
    {
        _dataValueEditorFactory = dataValueEditorFactory;
        _ioHelper = ioHelper;
        _textService = textService;
        _shortStringHelper = shortStringHelper;
        _jsonSerializer = jsonSerializer;
    }

    /// <inheritdoc />
    public override JsonTypeInfo Create(JsonElement json, JsonSerializerOptions options)
    {
        // in PackageManifest, property editors are IConfiguredDataEditor[] whereas
        // parameter editors are IDataEditor[] - both will end up here because we handle
        // IDataEditor and IConfiguredDataEditor implements it, but we can check the
        // type to figure out what to create
        EditorType type = EditorType.PropertyValue;

        if (json.TryGetProperty("propertyEditors", out JsonElement editors))
        {
            if (json.TryGetProperty("isParameterEditor", out JsonElement isParameterEditor) && isParameterEditor.GetBoolean())
            {
                type |= EditorType.MacroParameter;
            }
        }
        else
        {
            // parameter editor
            type = EditorType.MacroParameter;
        }

        JsonTypeInfo info = JsonTypeInfo.CreateJsonTypeInfo<IDataEditor>(options);
        info.CreateObject = () => new DataEditor(_dataValueEditorFactory, type);
        return info;
    }

    public override IDataEditor? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        JsonElement json = JsonElement.ParseValue(ref reader);
        DefaultJsonTypeInfoResolver resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(m => Create(json, options));
        options.TypeInfoResolver = resolver;
        JsonObject? node = JsonObject.Create(json);
        if (node == null)
        {
            return null;
        }

        DataEditor? editor = null;
        if (node["isPropertyEditor"] is JsonNode isPropEditor && isPropEditor.GetValue<bool>())
        {
            PrepareForPropertyEditor(node);
            editor = JsonSerializer.Deserialize<DataEditor>(node);
            if(editor != null)
            {
                editor.ExplicitConfigurationEditor = editor?.DefaultConfiguration == null ? null : new ConfigurationEditor();
            }
        }
        else
        {
            PrepareForParameterEditor(node);
            editor = JsonSerializer.Deserialize<DataEditor>(node);
        }

        if (editor != null && editor.ExplicitValueEditor == null)
        {
            editor.ExplicitValueEditor = new DataValueEditor(_textService, _shortStringHelper, _jsonSerializer);
        }

        return editor;
    }

    private static JsonArray RewriteValidators(JsonObject validation)
    {
        var jarray = new JsonArray();

        foreach (KeyValuePair<string, JsonNode?> v in validation)
        {
            var key = v.Key;
            JsonNode? val = v.Value;
            var jo = new JsonObject { { "type", key }, { "configuration", val } };
            jarray.Add(jo);
        }

        return jarray;
    }

    private void PrepareForPropertyEditor(JsonObject jobject)
    {
        if (jobject["editor"] == null)
        {
            throw new InvalidOperationException("Missing 'editor' value.");
        }

        if (jobject[SupportsReadOnly] is null)
        {
            jobject[SupportsReadOnly] = false;
        }

        // in the manifest, validators are a simple dictionary eg
        // {
        //   required: true,
        //   regex: '\\d*'
        // }
        // and we need to turn this into a list of IPropertyValidator
        // so, rewrite the json structure accordingly
        if (jobject["editor"]?["validation"] is JsonObject validation)
        {
            jobject["editor"]!["validation"] = RewriteValidators(validation);
        }

        if (jobject["editor"]?["view"] is JsonValue view)
        {
            jobject["editor"]!["view"] = RewriteVirtualUrl(view);
        }

        var prevalues = jobject["prevalues"] as JsonObject;
        var defaultConfig = jobject["defaultConfig"] as JsonObject;
        if (prevalues != null || defaultConfig != null)
        {
            // explicitly assign a configuration editor of type ConfigurationEditor
            // (else the deserializer will try to read it before setting it)
            // (and besides it's an interface)


            var config = new JsonObject();
            if (prevalues != null)
            {
                config = prevalues;

                // see note about validators, above - same applies to field validators
                if (config["fields"] is JsonArray jarray)
                {
                    foreach (JsonNode? field in jarray)
                    {
                        if (field == null)
                            continue;

                        if (field["validation"] is JsonObject fvalidation)
                        {
                            field["validation"] = RewriteValidators(fvalidation);
                        }

                        if (field["view"] is JsonValue fview)
                        {
                            field["view"] = RewriteVirtualUrl(fview);
                        }
                    }
                }
            }

            // in the manifest, default configuration is at editor level
            // move it down to configuration editor level so it can be deserialized properly
            if (defaultConfig != null)
            {
                config["defaultConfig"] = defaultConfig;
                jobject.Remove("defaultConfig");
            }

            // in the manifest, configuration is named 'prevalues', rename
            // it is important to do this LAST
            jobject["config"] = config;
            jobject.Remove("prevalues");
        }
    }

    private string? RewriteVirtualUrl(JsonValue view) => _ioHelper.ResolveRelativeOrVirtualUrl(view.GetValue<string>());

    private void PrepareForParameterEditor(JsonObject jobject)
    {
        // in a manifest, a parameter editor looks like:
        //
        // {
        //   "alias": "...",
        //   "name": "...",
        //   "view": "...",
        //   "config": { "key1": "value1", "key2": "value2" ... }
        // }
        //
        // the view is at top level, but should be down one level to be properly
        // deserialized as a ParameterValueEditor property -> need to move it
        if (jobject["view"] != null)
        {
            // move the 'view' property
            jobject["editor"] = new JsonObject { ["view"] = jobject["view"] };
            jobject.Remove("view");
        }

        if (jobject[SupportsReadOnly] is null)
        {
            jobject[SupportsReadOnly] = false;
        }

        // in the manifest, default configuration is named 'config', rename
        if (jobject["config"] is JsonObject config)
        {
            jobject["defaultConfig"] = config;
            jobject.Remove("config");
        }

        // We need to null check, if view do not exists, then editor do not exists
        if (jobject["editor"]?["view"] is JsonValue view)
        {
            jobject["editor"]!["view"] = RewriteVirtualUrl(view);
        }
    }


}
