using System.Text.Json.Serialization.Metadata;
using System.Text.Json;
using Umbraco.Cms.Core.Dashboards;
using Umbraco.Cms.Infrastructure.Serialization;

namespace Umbraco.Cms.Core.Manifest;

/// <summary>
///     Implements a json read converter for <see cref="IAccessRule" />.
/// </summary>
internal class DashboardAccessRuleConverter : JsonNetReadConverter<IAccessRule>
{
    public override JsonTypeInfo Create(JsonElement json, JsonSerializerOptions options) => JsonTypeInfo.CreateJsonTypeInfo<IAccessRule>(options);

    public override IAccessRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        JsonElement json = JsonElement.ParseValue(ref reader);
        IAccessRule accessRule = new AccessRule();
        GetRule(accessRule, json, "grant", AccessRuleType.Grant);
        GetRule(accessRule, json, "deny", AccessRuleType.Deny);
        GetRule(accessRule, json, "grantBySection", AccessRuleType.GrantBySection);

        if (accessRule.Type == AccessRuleType.Unknown)
        {
            throw new InvalidOperationException("Rule is not defined.");
        }

        return accessRule;
    }

    private void GetRule(IAccessRule rule, JsonElement json, string name, AccessRuleType type)
    {
        if (rule.Type != AccessRuleType.Unknown)
        {
            throw new InvalidOperationException("Multiple definition of a rule.");
        }

        if (json.TryGetProperty(name, out JsonElement value))
        {
            throw new InvalidOperationException("Rule value is not present in json.");
        }

        rule.Type = type;
        rule.Value = value.GetString();
    }
}
