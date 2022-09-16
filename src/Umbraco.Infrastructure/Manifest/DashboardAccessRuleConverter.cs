using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Dashboards;
using Umbraco.Cms.Core.Exceptions;
using Umbraco.Cms.Infrastructure.Serialization;

namespace Umbraco.Cms.Core.Manifest;

/// <summary>
///     Implements a json read converter for <see cref="IAccessRule" />.
/// </summary>
internal class DashboardAccessRuleConverter : SystemTextJsonReadConverter<IAccessRule>
{
    protected override Func<IAccessRule> Create(JsonElement json) => () => new AccessRule();

    protected override void FillProperties(IAccessRule target, JsonElement json, JsonSerializerOptions options)
    {
        if (target is not AccessRule accessRule)
        {
            throw new PanicException("panic.");
        }

        GetRule(accessRule, json, "grant", AccessRuleType.Grant);
        GetRule(accessRule, json, "deny", AccessRuleType.Deny);
        GetRule(accessRule, json, "grantBySection", AccessRuleType.GrantBySection);

        if (accessRule.Type == AccessRuleType.Unknown)
        {
            throw new InvalidOperationException("Rule is not defined.");
        }
    }

    private void GetRule(AccessRule rule, JsonElement json, string name, AccessRuleType type)
    {
        JsonElement element = json.GetProperty(name);
        if (rule.Type != AccessRuleType.Unknown)
        {
            throw new InvalidOperationException("Multiple definition of a rule.");
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Rule value is not a string.");
        }

        rule.Type = type;
        rule.Value = element.GetString();
    }
}
