# Stepwise Builder Generator

A lightweight C# source generator that produces strongly-typed, stepwise “fluent” builders. Simply annotate a class and describe its steps; the generator emits interfaces and a partial class to guide callers through each required step.

---

## Features

- **`AddStep<T>(stepName, fieldName = null)`**  
  Define each required input in order.

- **Default-value support**  
  Supply a factory for a step so callers can use `.StepName()` with no arguments.

- **`BranchFrom(baseBuilder, baseStep)`**  
  Insert an alternate path from one builder into another.

- **Enum of steps**  
  Every generated builder includes `enum Steps { … }` for logging or reflection.

- **Static factories**  
  `StepwiseBuilders.YourBuilder()` to kick off a chain.

---

## Examples

### 1. Basic builder  

**Target type**:
```csharp
public class Order
{
    public int Id { get; init; }
    public string Customer { get; init; }
    public decimal Total { get; init; }
}
```

**Builder declaration**:
```csharp
[StepwiseBuilder]
public partial class OrderBuilder
{
    public OrderBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetId",    "Id")
            .AddStep<string>("SetCustomer")
            .AddStep<decimal>("SetTotal")
            .CreateBuilderFor<Order>();
    }
}
```

**Usage**:
```csharp
var order = StepwiseBuilders.OrderBuilder()
    .SetId(123)
    .SetCustomer("Acme Co.")
    .SetTotal(99.95m)
    .Build(o => o);
```

---

### 2. Default-value steps  

**Target type**:
```csharp
public class ReportConfig
{
    public string Title        { get; init; }
    public bool   IncludeCharts{ get; init; }
}
```

**Builder declaration**:
```csharp
[StepwiseBuilder]
public partial class ReportConfigBuilder
{
    public ReportConfigBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("WithTitle")
            .AddStep<bool>("IncludeCharts", defaultValueFactory: () => true)
            .CreateBuilderFor<ReportConfig>();
    }
}
```

**Usage**:
```csharp
// uses default IncludeCharts = true
var config = StepwiseBuilders.ReportConfigBuilder()
    .WithTitle("Q1 Results")
    .IncludeCharts()   // no arg → defaultValueFactory invoked
    .Build(c => c);
```

---

### 3. Branching between builders  

**Base builder** (`UserBuilder`):
```csharp
[StepwiseBuilder]
public partial class UserBuilder
{
    public UserBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName")
            .AddStep<int>("SetAge")
            .CreateBuilderFor<User>();
    }
}
```

**Branching builder** (`VipUserBuilder`):
```csharp
[StepwiseBuilder]
public partial class VipUserBuilder
{
    public VipUserBuilder()
    {
        GenerateStepwiseBuilder
            .BranchFrom("UserBuilder", "SetAge")
            .AddStep<string>("SetMembershipLevel")
            .CreateBuilderFor<VipUser>();
    }
}
```

**Usage**:
```csharp
// regular user
var u1 = StepwiseBuilders.UserBuilder()
    .SetName("Alice")
    .SetAge(30)
    .Build(u => u);

// VIP user branches in after SetAge
var vip = StepwiseBuilders.UserBuilder()
    .SetName("Bob")
    .SetAge(45)
    .SetMembershipLevel("Gold")
    .Build(v => v);
```

---

## FAQ

**Q: Can I inject services or dependencies into a builder?**  
A: Yes—since the generated builders are `partial` classes, you’re free to add your own constructor(s) (e.g. taking `ILogger`, `IRepository`, etc.) in another `partial` file. Dependency-injected fields or properties will be available when the step methods run.

**Q: What if I omit the `fieldName` in `AddStep`?**  
A: A field named `{StepName}Value` is generated automatically.

**Q: How do I supply custom “Build” logic?**  
A: Call `.Build(instance => /* your mapping to target */)` or add extension methods on the final build interface for reusable patterns.
