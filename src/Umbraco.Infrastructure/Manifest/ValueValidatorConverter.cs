using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Infrastructure.Serialization;

namespace Umbraco.Cms.Core.Manifest;

/// <summary>
///     Implements a json read converter for <see cref="IValueValidator" />.
/// </summary>
internal class ValueValidatorConverter : SystemTextJsonReadConverter<IValueValidator>
{
    private readonly ManifestValueValidatorCollection _validators;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ValueValidatorConverter" /> class.
    /// </summary>
    public ValueValidatorConverter(ManifestValueValidatorCollection validators) => _validators = validators;

    protected override Func<IValueValidator> Create(JsonElement json)
    {
        string? type = json.GetProperty("type").GetString();
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new InvalidOperationException("Could not get the type of the validator.");
        }

        return () => _validators.GetByName(type);
    }

    protected override void FillProperties(IValueValidator target, JsonElement json, JsonSerializerOptions options)
    {
        return;
    }
}
