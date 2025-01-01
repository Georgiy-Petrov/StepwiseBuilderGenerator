namespace StepwiseBuilderGenerator.DTOs;

record SidePathInfo(string BaseBuilderName, string BaseBuilderStep)
{
    public string BaseBuilderName { get; } = BaseBuilderName;
    public string BaseBuilderStep { get; } = BaseBuilderStep;
}