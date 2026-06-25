using SharpYaml.Serialization;

namespace OpenApiTools.Models;

public sealed class LintConfig
{
    public Dictionary<string, LintRuleConfig> Rules { get; set; } = new();
}

public sealed class LintRuleConfig
{
    public string Severity { get; set; } = "error";
    public Dictionary<string, object>? Options { get; set; }
}

public static class LintConfigLoader
{
    public static LintConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Lint config not found: " + path, path);

        var yaml = File.ReadAllText(path);
        var serializer = new Serializer();
        var raw = serializer.Deserialize<Dictionary<string, object>>(yaml)
            ?? throw new InvalidOperationException("Failed to parse lint config.");

        var config = new LintConfig();
        if (raw.TryGetValue("rules", out var rulesObj) && rulesObj is IDictionary<object, object> rulesDict)
        {
            foreach (var (ruleNameObj, ruleObj) in rulesDict)
            {
                var ruleName = ruleNameObj?.ToString() ?? "";
                var ruleConfig = new LintRuleConfig();
                if (ruleObj is IDictionary<object, object> ruleDict)
                {
                    if (ruleDict.TryGetValue("severity", out var sev))
                        ruleConfig.Severity = sev?.ToString() ?? "error";
                    if (ruleDict.TryGetValue("options", out var opts) && opts is IDictionary<object, object> optsDict)
                        ruleConfig.Options = optsDict.ToDictionary(kv => kv.Key?.ToString() ?? "", kv => kv.Value);
                }
                config.Rules[ruleName] = ruleConfig;
            }
        }

        return config;
    }
}