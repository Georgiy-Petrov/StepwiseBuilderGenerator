namespace StepwiseBuilderGenerator;

record StepMethod(int Order, string StepName, string? FieldName, string Type)
{
    public int Order { get; } = Order;
    public string StepName { get; } = StepName;
    public string? FieldName { get; } = FieldName;
    public string Type { get; } = Type;
}