using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Umbraco.Cms.Infrastructure.Serialization
{
    public abstract class SystemTextJsonReadConverter<T> : JsonConverter<T> where T : notnull
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (typeof(T).IsAssignableFrom(typeToConvert))
                return true;

            return false;
        }
        protected abstract Func<T> Create(JsonElement json);

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonElement json = JsonElement.ParseValue(ref reader);
            JsonObjectInfoValues<T> objectInfo = new JsonObjectInfoValues<T>()
            {
                ObjectCreator = Create(json)
            };

            JsonTypeInfo<T> jsonTypeInfo = JsonMetadataServices.CreateObjectInfo(options, objectInfo);
            T? target = JsonSerializer.Deserialize<T>(json, jsonTypeInfo);
            if(target is null)
            {
                return default;
            }

            FillProperties(target, json, options);
            return target;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => throw new NotSupportedException("This JsonConverter is read only");

        protected abstract void FillProperties(T target, JsonElement json, JsonSerializerOptions options);
    }
}
