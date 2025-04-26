---

# Stepwise Builder Generator 🚀

Lightweight, compile-time-safe C# source generator for fluent stepwise builders.  
Forces correct data collection without runtime cost or reflection.

[![NuGet](https://img.shields.io/nuget/v/StepwiseBuilderGenerator.svg?logo=nuget)](https://www.nuget.org/packages/StepwiseBuilderGenerator)

---

## ✨ Overview

Building complex objects safely often requires enforcing a strict order of steps.  
**Stepwise Builder Generator** produces fluent builder APIs automatically from simple annotations.

It generates strongly-typed interfaces, fields, default values, and overloads,  
ensuring you can't forget required data or call steps out of order.

You retain full control over how the final object is built — the Build method always delegates to your provided logic.
You define exactly how the collected data is transformed, allowing uses beyond object construction,  
such as building complex data pipelines, aggregators, or workflows.

No runtime reflection. No manual boilerplate.

---

## 🚀 Quick Start

Define your target type:

```csharp
public class Order
{
    public int Id { get; init; }
    public string Customer { get; init; }
    public decimal Total { get; init; }
}
```

Create a builder:

```csharp
[StepwiseBuilder]
public partial class OrderBuilder
{
    public OrderBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetId", "Id")
            .AddStep<string>("SetCustomer")
            .AddStep<decimal>("SetTotal")
            .CreateBuilderFor<Order>(b => new Order
            {
                Id = b.Id,
                Customer = b.SetCustomerValue,
                Total = b.SetTotalValue
            });
    }
}
```

Use it:

```csharp
var order = StepwiseBuilders.OrderBuilder()
    .SetId(1001)
    .SetCustomer("Acme Co.")
    .SetTotal(250.75m)
    .Build();
```

---

## 🛠️ Key Features

- **Stepwise typing:** Force caller to supply each field in order
- **Default values:** Define steps with default factories
- **Overloads:** Accept alternative types with mapping
- **Branching:** Extend another builder at a specific step
- **Static factories:** Entry points like `StepwiseBuilders.OrderBuilder()`
- **Enum of steps:** Generated `Steps` enum for diagnostics or logging
- **User-defined Build:** You fully control how data is assembled
- **Zero runtime cost:** Pure compile-time source generation

---

## 📚 Examples

### 1️⃣ Default-Value Step

```csharp
public class ReportConfig
{
    public string Title { get; init; }
    public bool IncludeCharts { get; init; }
}
```

```csharp
[StepwiseBuilder]
public partial class ReportConfigBuilder
{
    public ReportConfigBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("WithTitle")
            .AddStep<bool>("IncludeCharts", () => true)
            .CreateBuilderFor<ReportConfig>(b => new ReportConfig
            {
                Title = b.WithTitleValue,
                IncludeCharts = b.IncludeChartsValue
            });
    }
}
```

Usage:

```csharp
var report = StepwiseBuilders.ReportConfigBuilder()
    .WithTitle("Annual Report")
    .IncludeCharts() // uses default true
    .Build();        // uses provided Build factory
```

---

### 2️⃣ Overload Mapping

```csharp
[StepwiseBuilder]
public partial class SimpleBuilder
{
    public SimpleBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetValue")
            .AndOverload<string, int>(valueAsString => int.Parse(valueAsString))
            // or int.Parse could be used directly as delegate.
            .CreateBuilderFor<int>();
    }
}
```

Usage:

```csharp
var value = StepwiseBuilders.SimpleBuilder()
    .SetValue("123")  // maps string to int
    .Build(b => b.SetValueValue);
```

---

### 3️⃣ Branching Between Builders

Base builder:

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

Branching builder:

```csharp
[StepwiseBuilder]
public partial class VipUserBuilder
{
    public VipUserBuilder()
    {
        GenerateStepwiseBuilder
            .BranchFrom<UserBuilder>("SetAge")
            .AddStep<string>("SetMembershipLevel")
            .CreateBuilderFor<VipUser>();
    }
}
```

Usage:

```csharp
var vipUser = StepwiseBuilders.UserBuilder()
    .SetName("Bob")
    .SetAge(45)
    .SetMembershipLevel("Gold") //After SetAge(), control transfers into VipUserBuilder steps via extension method.
    .Build(b => new VipUser
    {
        Name = b.OriginalBuilder.SetNameValue,
        Age = b.OriginalBuilder.SetAgeValue,
        MembershipLevel = b.SetMembershipLevelValue
    });
```

---

## ❓ FAQ

**Q: Can I inject dependencies into builders?**  
A: Yes. Builders are partial classes. You can define additional constructors in another partial file.

**Q: What happens if I omit `fieldName` in `AddStep()`?**  
A: A field named `{StepName}Value` is automatically created.

**Q: Can I customize the Build() process?**  
A: Yes. You must define Build() logic. No Build method is auto-generated without your code.

**Q: Can it be used for things beyond object construction?**  
A: Absolutely. Builders can be used for data aggregation, pipelines, workflows, or any ordered data collection.

**Q: Does this add any runtime cost?**  
A: No. Everything happens at compile-time. No reflection or dynamic code at runtime.

---

## 📄 License

Licensed under the MIT License.

---
