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
    (string, string)? CreateBuilderForDefaultValueFactoryInfo,
    EquatableArray<StepInfoOverloadInfo>? StepInfosOverloads)
{
    public string DeclaredNamespace { get; } = DeclaredNamespace;
    public EquatableArray<string>? Usings { get; } = Usings;
    public string TargetTypeName { get; } = TargetTypeName;
    public EquatableArray<StepInfo> StepMethods { get; } = StepMethods;
    public EquatableArray<StepInfoOverloadInfo>? StepInfosOverloads { get; } = StepInfosOverloads;
    public string ClassName { get; } = ClassName;
    public (string, string) TypeParametersAndConstraints { get; } = TypeParametersAndConstraints;
    public SidePathInfo? SidePath { get; } = SidePath;
    public (string, string)? CreateBuilderForDefaultValueFactoryInfo { get; } = CreateBuilderForDefaultValueFactoryInfo;
}

internal record StepInfoOverloadInfo(string StepName, string ParameterType, string ReturnType, string Mapper, string? OverloadMethodName)
{
    public string StepName { get; } = StepName;
    public string ParameterType { get; } = ParameterType;
    public string ReturnType { get; } = ReturnType;
    public string Mapper { get; } = Mapper;
    public string? OverloadMethodName { get; } = OverloadMethodName;
}