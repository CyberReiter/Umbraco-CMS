using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models.ContentEditing;

namespace Umbraco.Cms.Infrastructure.Serialization
{
    public abstract class JsonNetReadConverter<T> : JsonConverter<T>
    {
        public override bool CanConvert(Type typeToConvert) => typeof(T).IsAssignableFrom(typeToConvert);

        public abstract JsonTypeInfo Create(JsonElement json, JsonSerializerOptions options);

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonElement json = JsonElement.ParseValue(ref reader);
            DefaultJsonTypeInfoResolver resolver = new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(m => Create(json, options));
            options.TypeInfoResolver = resolver;
            return JsonSerializer.Deserialize<T>(json, options);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => throw new NotImplementedException("This converter is readonly!");
    }
}
