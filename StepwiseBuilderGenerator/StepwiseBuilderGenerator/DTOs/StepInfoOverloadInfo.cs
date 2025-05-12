namespace StepwiseBuilderGenerator.DTOs;

internal record StepInfoOverloadInfo(string StepName, string ParameterType, string ReturnType, string Mapper, string? OverloadMethodName)
{
    public string StepName { get; } = StepName;
    public string ParameterType { get; } = ParameterType;
    public string ReturnType { get; } = ReturnType;
    public string Mapper { get; } = Mapper;
    public string? OverloadMethodName { get; } = OverloadMethodName;
}