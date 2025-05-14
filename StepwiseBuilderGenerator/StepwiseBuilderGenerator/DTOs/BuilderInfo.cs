using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator.DTOs;

internal record BuilderInfo(
    EquatableArray<string>? Usings,
    string TargetTypeName,
    EquatableArray<EquatableArray<string>> ContainingTypeConstructorsParameters,
    EquatableArray<StepInfo> StepMethods,
    EquatableArray<StepInfo>? StepMethodsWithDefaultValueFactory,
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
    public EquatableArray<EquatableArray<string>> ContainingTypeConstructorsParameters { get; } = ContainingTypeConstructorsParameters;
    public EquatableArray<StepInfo> StepMethods { get; } = StepMethods;
    public EquatableArray<StepInfo>? StepMethodsWithDefaultValueFactory { get; } = StepMethodsWithDefaultValueFactory;
    public EquatableArray<StepInfoOverloadInfo>? StepInfosOverloads { get; } = StepInfosOverloads;
    public string ClassName { get; } = ClassName;
    public (string, string) TypeParametersAndConstraints { get; } = TypeParametersAndConstraints;
    public SidePathInfo? SidePath { get; } = SidePath;
    public (string, string)? CreateBuilderForDefaultValueFactoryInfo { get; } = CreateBuilderForDefaultValueFactoryInfo;
}