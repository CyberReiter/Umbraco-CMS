using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPoco.fastJSON;
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
internal class DataEditorConverter : SystemTextJsonReadConverter<IDataEditor>
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
    protected override Func<IDataEditor> Create(JsonElement json)
    {
        // in PackageManifest, property editors are IConfiguredDataEditor[] whereas
        // parameter editors are IDataEditor[] - both will end up here because we handle
        // IDataEditor and IConfiguredDataEditor implements it, but we can check the
        // type to figure out what to create
        EditorType type = EditorType.PropertyValue;
        if (json.TryGetProperty("isParameterEditor", out JsonElement value) && value.GetBoolean() == true)
        {
            type |= EditorType.MacroParameter;
            return () => new DataEditor(_dataValueEditorFactory, type);
        }

        type = EditorType.MacroParameter;
        return () => new DataEditor(_dataValueEditorFactory, type);
    }

    protected override void FillProperties(IDataEditor target, JsonElement json, JsonSerializerOptions options)
    {
        //if (!(target is DataEditor dataEditor))
        //{
        //    throw new Exception("panic.");
        //}

        //if (json.GetProperty("propertyEditors").ValueKind == JsonValueKind.Array)
        //{
        //    PrepareForPropertyEditor(json, dataEditor);
        //}
        //else
        //{
        //    PrepareForParameterEditor(json, dataEditor);
        //}


    }

    private static JsonArray RewriteValidators(JsonNode validation)
    {
        JsonArray jArray = new JsonArray();
        //foreach (KeyValuePair<string, JToken?> v in validation.AsValue().)
        //{
        //    var key = v.Key;
        //    JToken? val = v.Value;
        //    var jo = new JObject { { "type", key }, { "configuration", val } };
        //    jArray.Add(jo);
        //}

        return jArray;
    }

    private void PrepareForPropertyEditor(JsonElement json, DataEditor target)
    {
        JsonObject? node = JsonObject.Create(json);
        if (node is null)
        {
            throw new InvalidOperationException("Invalid editor json");
        }

        if (node["editor"] is null)
        {
            throw new InvalidOperationException("Missing 'editor' value.");
        }

        if (node[SupportsReadOnly] is null)
        {
            node[SupportsReadOnly] = false;
        }

        // explicitly assign a value editor of type ValueEditor
        // (else the deserializer will try to read it before setting it)
        // (and besides it's an interface)
        target.ExplicitValueEditor = new DataValueEditor(_textService, _shortStringHelper, _jsonSerializer);

        // in the manifest, validators are a simple dictionary eg
        // {
        //   required: true,
        //   regex: '\\d*'
        // }
        // and we need to turn this into a list of IPropertyValidator
        // so, rewrite the json structure accordingly
        JsonNode? validation = node["editor"]?["validation"];
        if (validation is not null)
        {
            node["editor"]!["validation"] = RewriteValidators(validation);
        }

        JsonNode? view = node["editor"]?["view"];
        if (view is not null)
        {
            node["editor"]!["view"] = RewriteVirtualUrl(view);
        }

        JsonNode? prevalues = node["prevalues"];
        JsonNode? defaultConfig = node["defaultConfig"];
        if (prevalues is not null || defaultConfig is not null)
        {
            // explicitly assign a configuration editor of type ConfigurationEditor
            // (else the deserializer will try to read it before setting it)
            // (and besides it's an interface)
            target.ExplicitConfigurationEditor = new ConfigurationEditor();

            var config = JsonValue.Create(prevalues);
            if (prevalues is not null && config is not null)
            {
                // see note about validators, above - same applies to field validators
                if (config["fields"] is JsonArray jarray)
                {
                    foreach (JsonNode? field in jarray)
                    {
                        if (field is null)
                            continue;

                        if (field["validation"] is JsonNode fvalidation)
                        {
                            field["validation"] = RewriteValidators(fvalidation);
                        }

                        if (field["view"] is JsonNode fview)
                        {
                            field["view"] = RewriteVirtualUrl(fview);
                        }
                    }
                }
            }

            // in the manifest, default configuration is at editor level
            // move it down to configuration editor level so it can be deserialized properly
            if (defaultConfig is not null && config is not null)
            {
                config["defaultConfig"] = defaultConfig;
                node.Remove("defaultConfig");
            }

            // in the manifest, configuration is named 'prevalues', rename
            // it is important to do this LAST
            node["config"] = config;
            node.Remove("prevalues");
        }
    }

    private string? RewriteVirtualUrl(JsonNode view) => _ioHelper.ResolveRelativeOrVirtualUrl(view.ToString());

    private void PrepareForParameterEditor(JsonElement json, DataEditor target)
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
        JsonObject? node = JsonObject.Create(json);
        if (node is not null)
        {
            if (node["view"] is not null)
            {
                // explicitly assign a value editor of type ParameterValueEditor
                target.ExplicitValueEditor = new DataValueEditor(_textService, _shortStringHelper, _jsonSerializer);

                // move the 'view' property
                node["editor"] = new JsonObject { ["view"] = node["view"] };
                node["view"] = null;
            }

            if (node[SupportsReadOnly] is null)
            {
                node[SupportsReadOnly] = false;
            }

            // in the manifest, default configuration is named 'config', rename
            if (node["config"] is JsonObject config)
            {
                node["defaultConfig"] = config;
                node.Remove("config");
            }

            // We need to null check, if view do not exists, then editor do not exists
            if (node["editor"]?["view"] is JsonValue view)
            {
                node["editor"]!["view"] = RewriteVirtualUrl(view);
            }
        }
    }
}
