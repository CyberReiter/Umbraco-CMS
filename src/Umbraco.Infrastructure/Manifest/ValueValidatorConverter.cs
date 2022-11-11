using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Infrastructure.Serialization;

namespace Umbraco.Cms.Core.Manifest;

/// <summary>
///     Implements a json read converter for <see cref="IValueValidator" />.
/// </summary>
internal class ValueValidatorConverter : JsonNetReadConverter<IValueValidator>
{
    private readonly ManifestValueValidatorCollection _validators;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ValueValidatorConverter" /> class.
    /// </summary>
    public ValueValidatorConverter(ManifestValueValidatorCollection validators) => _validators = validators;

    public override JsonTypeInfo Create(JsonElement json, JsonSerializerOptions options)
    {
        if (json.TryGetProperty("type", out JsonElement value))
        {
            string? type = value.GetString();
            if (!string.IsNullOrWhiteSpace(type))
            {
                JsonTypeInfo info = JsonTypeInfo.CreateJsonTypeInfo<IValueValidator>(options);
                info.CreateObject = () => _validators.GetByName(type);
                return info;
            }
        }

        throw new InvalidOperationException("Could not get the type of the validator.");
    }
}
