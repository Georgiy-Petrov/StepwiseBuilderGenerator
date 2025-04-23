using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator.DTOs;

internal record BuilderInfo(
    EquatableArray<string>? Usings,
    string TargetTypeName,
    EquatableArray<StepInfo> StepMethods,
    string ClassName,
    (string, string) TypeParametersAndConstraints,
    SidePathInfo? SidePath,
    string DeclaredNamespace, 
    (string, string)? CreateBuilderForDefaultValueFactoryInfo)
{
    public string DeclaredNamespace { get; } = DeclaredNamespace;
    public EquatableArray<string>? Usings { get; } = Usings;
    public string TargetTypeName { get; } = TargetTypeName;
    public EquatableArray<StepInfo> StepMethods { get; } = StepMethods;
    public string ClassName { get; } = ClassName;
    public (string, string) TypeParametersAndConstraints { get; } = TypeParametersAndConstraints;
    public SidePathInfo? SidePath { get; } = SidePath;
    
    public (string, string)? CreateBuilderForDefaultValueFactoryInfo { get; } = CreateBuilderForDefaultValueFactoryInfo;
}