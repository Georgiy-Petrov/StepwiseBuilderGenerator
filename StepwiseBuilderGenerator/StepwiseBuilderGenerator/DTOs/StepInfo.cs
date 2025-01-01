namespace StepwiseBuilderGenerator.DTOs;

public record StepInfo(int Order, string StepName, string? FieldName, string ParameterType)
{
    public int Order { get; } = Order;
    public string StepName { get; } = StepName;
    public string? FieldName { get; } = FieldName;
    public string ParameterType { get; } = ParameterType;
}