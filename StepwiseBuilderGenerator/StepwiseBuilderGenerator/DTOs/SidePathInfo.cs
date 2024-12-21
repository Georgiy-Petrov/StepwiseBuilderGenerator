namespace StepwiseBuilderGenerator.DTOs;

record SidePathInfo(string BuilderToExtendName, string StepToExtendName)
{
    public string BuilderToExtendName { get; } = BuilderToExtendName;
    public string StepToExtendName { get; } = StepToExtendName;
}