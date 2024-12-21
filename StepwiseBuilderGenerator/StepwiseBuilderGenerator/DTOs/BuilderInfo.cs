using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator.DTOs;

internal record BuilderInfo
{
    internal BuilderInfo(EquatableArray<string>? Usings, string TargetType, EquatableArray<StepMethodInfo> AddStepInfos, string ClassName, (string, string) ClassTypeParametersAndConstraints, SidePathInfo? SidePathInfo, (string, EquatableArray<StepMethodInfo>) ClassDeclarationAndStepsOfBuilderToExtend, string Namespace)
    {
        this.ClassDeclarationAndStepsOfBuilderToExtend = ClassDeclarationAndStepsOfBuilderToExtend;
        this.Usings = Usings;
        this.TargetType = TargetType;
        this.AddStepInfos = AddStepInfos;
        this.ClassName = ClassName;
        this.ClassTypeParametersAndConstraints = ClassTypeParametersAndConstraints;
        this.SidePathInfo = SidePathInfo;
        this.Namespace = Namespace;
    }

    public string Namespace { get; }
    public EquatableArray<string>? Usings { get; }
    public string TargetType { get; }
    public EquatableArray<StepMethodInfo> AddStepInfos { get; }
    public string ClassName { get; }
    public (string, string) ClassTypeParametersAndConstraints { get; }
    public SidePathInfo? SidePathInfo { get; }
    public (string, EquatableArray<StepMethodInfo>) ClassDeclarationAndStepsOfBuilderToExtend { get; }
}