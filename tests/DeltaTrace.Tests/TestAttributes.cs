namespace DeltaTrace;

// Mock attributes for testing purposes
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class DeltaTraceAttribute : Attribute
{
    public bool DeepTracking { get; set; } = true;
    public bool GenerateExtensions { get; set; } = true;
    public bool GenerateJsonSupport { get; set; } = false;
    public string DeltaSuffix { get; set; } = "Delta";
}

[AttributeUsage(AttributeTargets.Property)]
public class IgnoreDeltaAttribute : Attribute
{
}