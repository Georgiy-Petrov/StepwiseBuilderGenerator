# Stepwise Builder Generator 🚀

A C# Source Generator that helps you define clear, guided processes for gathering information and initiating actions, all at compile-time.
It creates fluent, type-safe APIs to ensure all necessary inputs are provided before your custom logic is executed, without runtime overhead or reflection.

[![NuGet](https://img.shields.io/nuget/v/StepwiseBuilderGenerator.svg?logo=nuget)](https://www.nuget.org/packages/StepwiseBuilderGenerator)

---

## 📜 Table of Contents

*   [✨ Overview](#-overview)
*   [🚀 Quick Start](#-quick-start)
*   [🛠️ Key Features](#-key-features)
    * [Configuration Api Summary](#configuration-api-summary)
*   [📚 Examples](#-examples)
    *   [1️⃣ Optional Input with Default Value Factory](#1-optional-input-with-default-value-factory)
    *   [2️⃣ Flexible Input Method with Mapping (Overload)](#2-flexible-input-method-with-mapping-overload)
    *   [3️⃣ Composing Guided Processes (Branching)](#3-composing-guided-processes-branching)
    *   [4️⃣ Generic Configuration](#4-generic-configuration)
    *   [5️⃣ Default Final Action (Parameterless Build)](#5-default-final-action-parameterless-build)
*   [❓ FAQ](#-faq)
*   [⚠️ Constraints and Considerations](#-constraints-and-considerations)
*   [📄 License](#-license)

---

## ✨ Overview

Constructing complex objects or orchestrating multi-faceted operations often benefits from a structured approach to gathering all necessary inputs. The **Stepwise Builder Generator** automates the creation of fluent APIs that guide users through providing this information, directly from simple configurations in your C# code.

It generates strongly-typed interfaces and methods, leading the user methodically through each required piece of information. This approach ensures completeness and correctness before the final, user-defined action is triggered.

The true power is in your hands: the final `.Build()` method receives all the collected data and executes *your* custom logic. This means you're not limited to simple object instantiation. You can define intricate construction processes, configure services, trigger complex workflows, or execute any series of operations based on the systematically gathered inputs.

**Key benefits:**

*   **Guided Input Provision:** Structures the way information is collected, making complex setups more manageable and less error-prone.
*   **Compile-time Completeness:** Ensures all defined data points are addressed before the final action can be invoked.
*   **Fluent and Intuitive API:** Makes providing necessary data straightforward for the API consumer.
*   **User-Defined Outcome:** You control the final result, whether it's creating an object, starting a process, or configuring a system.
*   **Zero Runtime Reflection:** All code is generated at compile-time.
*   **Reduced Boilerplate:** Eliminates the need to manually write intricate data-gathering logic.

---

## 🚀 Quick Start

1.  **Install the NuGet package:**
    ```bash
    dotnet add package StepwiseBuilderGenerator
    ```

2.  **Define your target type:**
    ```csharp
    public class Order
    {
        public int Id { get; init; }
        public string CustomerName { get; init; }
        public decimal TotalAmount { get; init; }
    }
    ```

3.  **Create a builder configuration class to define the information-gathering process:**
    ```csharp
    [StepwiseBuilder]
    public partial class OrderBuilder
    {
        // Parameterless constructor is required for the generator
        public OrderBuilder()
        {
            GenerateStepwiseBuilder                    // Begin defining the input process
                .AddStep<int>("WithId")                // Define steps to provide information
                .AddStep<string>("ForCustomer")        
                .AddStep<decimal>("WithTotal")         
                .CreateBuilderFor<Order>();            // Specify the type this process aims to build or act upon.
                                                       // (Collected data stored in fields: WithIdValue, ForCustomerValue, WithTotalValue)
        }
    }
    ```

4.  **Use the generated guided API:**
    ```csharp
    public class OrderService
    {
        public Order CreateNewOrder()
        {
            // StepwiseBuilders provides an entry point to the guided process
            var order = StepwiseBuilders.OrderBuilder()
                .WithId(1001)
                .ForCustomer("Acme Corp")
                .WithTotal(250.75m)
                // The .Build() method takes your logic to finalize the operation
                .Build(builder => new Order // You define what happens with the collected data
                {
                    Id = builder.WithIdValue,
                    CustomerName = builder.ForCustomerValue,
                    TotalAmount = builder.WithTotalValue
                });

            return order;
        }
    }
    ```

---

## 🛠️ Key Features

*   **Guided Method Chaining:** Structures data provision through a sequence of distinct, type-safe methods, ensuring each piece of information is considered.
*   **Ensured Completeness:** Guarantees all data points defined via `AddStep` are addressed before the final action.
*   **Optional Inputs with Defaults:** Provide a factory function to `AddStep` to specify a default if an input isn't explicitly given.
*   **Input Field Customization:** Optionally name the internal field for an input's value in `AddStep<TValue>("MethodName", "fieldName")`. Defaults to `MethodNameValue`.
*   **Flexible Input Methods (Overloads):** Use `.AndOverload<TInput, TOriginalParam>(mapperFunc)` to offer alternative ways to supply data for a preceding input point, with automatic type mapping.
*   **Composable Processes (Branching):** Use `.BranchFromStepBefore<BaseBuilder>("AtInputPointName")` to create a new guided process that extends or diverges from an existing one at a specific input collection point.
*   **Generic Configurations & Constraints:** Define configuration classes with generic type parameters and constraints, which are propagated to the generated API.
*   **Static Entry Points:** Root configurations (not using `BranchFromStepBefore`) get a static method in the `StepwiseBuilders` class (e.g., `StepwiseBuilders.YourBuilderName()`) to start the process.
*   **User-Defined Final Action:**
    *   You *must* provide the logic for the final operation by passing a `Func<YourBuilder, YourTargetType>` to the `.Build(...)` call.
    *   Optionally, provide a default action factory to `CreateBuilderFor<YourBuilderType, YourTargetType>(yourDefaultFactory)` or `CreateBuilderFor<YourTargetType>(yourDefaultFactory)`. This can enable a parameterless `Build()` extension method.
*   **`Steps` Enum:** A public `enum Steps` is generated, listing all defined input points without a default value, useful for diagnostics.
*   **Zero Runtime Cost:** Pure compile-time C# source generation.

### Configuration API Summary

The core of defining a stepwise builder is through the fluent API starting with `GenerateStepwiseBuilder` inside your configuration class's constructor:

| Method / Option                                                 | Purpose                                                                                                                 |
|:----------------------------------------------------------------|:------------------------------------------------------------------------------------------------------------------------|
| `AddStep<TValue>("MethodName")`                                 | Defines a mandatory input point. Generates method `MethodName(TValue value)`. Value stored in `MethodNameValue`.        |
| `AddStep<TValue>("MethodName", "fieldName")`                    | Defines a mandatory input point. Generates method `MethodName(TValue value)`. Value stored in `{customFieldName}`.      |
| `AddStep<TValue>(..., defaultValueFactory: () => ...)`          | Defines an skippable input point with a default value factory if not explicitly set.                                    |
| `AndOverload<TInput, TOriginalParam>(mapperFunc)`               | Adds an alternative input method for the preceding `AddStep`, mapping `TInput` to `TOriginalParam`.                     |
| `AndOverload<..., TOriginalParam>("NewName", ...)`              | Same as above, but allows specifying a `NewName` for the overload method.                                               |
| `BranchFromStepBefore<TBaseBuilder>("BaseStepName")`            | Starts a new guided path, branching off `TBaseBuilder` before its `BaseStepName` input point.                           |
| `CreateBuilderFor<TTarget>()`                                   | Finalizes the configuration, specifying `TTarget` as the outcome type. Requires a `Build(Func<...>)` call.              |
| `CreateBuilderFor<TBuilder, TTarget>(factory)`                  | Finalizes configuration, providing a default factory for a parameterless `Build()` if `TBuilder` matches current class. |

---

## 📚 Examples

### 1️⃣ Optional Input with Default Value Factory

Define an input point that has a default value if not explicitly provided.

```csharp
public class ReportConfig
{
    public string Title { get; init; }
    public bool IncludeHeader { get; init; }
}

[StepwiseBuilder]
public partial class ReportConfigBuilder
{
    public ReportConfigBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("WithTitle")
            // Provide a factory for the default for IncludeHeader
            .AddStep<bool>("ShouldIncludeHeader", defaultValueFactory: () => true)
            .CreateBuilderFor<ReportConfig>();
    }
}

// Usage:
var reportWithDefaultHeader = StepwiseBuilders.ReportConfigBuilder()
    .WithTitle("Annual Summary")
    // .ShouldIncludeHeader(value) is not called, so the default (true) is used.
    .Build(b => new ReportConfig { Title = b.WithTitleValue, IncludeHeader = b.ShouldIncludeHeaderValue });
```

### 2️⃣ Flexible Input Method with Mapping (Overload)

Accept an alternative input type and map it.

```csharp
[StepwiseBuilder]
public partial class DataPointBuilder
{
    public DataPointBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetValue") // Original input method expects an int
            // Add an alternative that accepts a string and parses it
            .AndOverload<string, int>(inputString => int.Parse(inputString))
            .CreateBuilderFor<int>();
    }
}

// Usage:
var dataPoint = StepwiseBuilders.DataPointBuilder()
    .SetValue("123") // Uses the string input alternative
    .Build(b => b.SetValueValue); // dataPoint is 123
```

### 3️⃣ Composing Guided Processes (Branching)

Extend one information-gathering flow with another.

```csharp
public class User { public string Name { get; init; } public int Age { get; init; } }
public class VipUser : User { public string MembershipLevel { get; init; } }

[StepwiseBuilder]
public partial class UserBuilder
{
    public UserBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("WithName")
            .AddStep<int>("WithAge")
            .CreateBuilderFor<User>();
    }
}

[StepwiseBuilder]
public partial class VipUserBuilder
{
    public VipUserBuilder()
    {
        GenerateStepwiseBuilder
            // Branch from UserBuilder before its 'WithAge' input point
            .BranchFromStepBefore<UserBuilder>("WithAge")
            .AddStep<int>("WithVipAge")
            .AddStep<string>("WithMembershipLevel") // Add VIP-specific input
            .CreateBuilderFor<VipUser>();
    }
}

// Usage:
var vipUser = StepwiseBuilders.UserBuilder() // Start with the base process
    .WithName("Alice")
    // Before .WithAge(), .WithVipAge() and .WithMembershipLevel() becomes available,
    // transitioning to the VipUserBuilder flow.
    .WithVipAge(30)
    .WithMembershipLevel("Gold")
    .Build(branchedBuilder => new VipUser // Your logic combines data from both
    {
        Name = branchedBuilder.OriginalBuilder.WithNameValue,
        Age = branchedBuilder.WithVipAgeValue,
        MembershipLevel = branchedBuilder.WithMembershipLevelValue
    });
```

### 4️⃣ Generic Configuration

```csharp
public class Container<T> { public T Content { get; init; } public string Label { get; init; } }

[StepwiseBuilder]
public partial class ContainerBuilder<TItem> where TItem : class
{
    public ContainerBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<TItem>("WithContent")
            .AddStep<string>("WithLabel", "LabelField")
            // Configure a default final action factory.
            .CreateBuilderFor<ContainerBuilder<TItem>, Container<TItem>>(
                builder => new Container<TItem> { Content = builder.WithContentValue, Label = builder.LabelField }
            );
    }
}

// Usage:
var stringContainer = StepwiseBuilders.ContainerBuilder<string>()
    .WithContent("Hello Generics")
    .WithLabel("Text Box")
    .Build(); // Parameterless Build() is available due to the default factory
```

### 5️⃣ Default Final Action (Parameterless Build)

Provide a default factory to `CreateBuilderFor` to enable a simpler, parameterless `.Build()` call.

```csharp
public class SimplePoco
{
    public string Name { get; init; }
    public int Value { get; init; }
}

[StepwiseBuilder]
public partial class SimplePocoBuilder
{
    public SimplePocoBuilder()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("WithName")
            .AddStep<int>("WithValue")
            // Provide the default build logic by passing a factory function to CreateBuilderFor.
            // The first generic argument <SimplePocoBuilder> matches this builder class,
            // enabling the parameterless .Build() extension.
            .CreateBuilderFor<SimplePocoBuilder, SimplePoco>(b => new SimplePoco { Name = b.WithNameValue, Value = b.WithValueValue });
    }
}

// Usage:
var poco = StepwiseBuilders.SimplePocoBuilder()
    .WithName("Test Item")
    .WithValue(42)
    .Build(); // Parameterless .Build() is available
```

---

## ❓ FAQ

**Q: Can I inject dependencies into the builder configuration class (e.g., `OrderBuilder`)?**  
A: Yes. While the generator requires a parameterless constructor for discovery of the configuration chain, you can define other constructors (e.g., for dependency injection) in your partial class. The StepwiseBuilders static class will generate corresponding factory methods for these constructors, allowing you to start the build process with dependencies injected. If you instantiate the builder directly via new YourBuilder(dependencies), see the "Initializing Default Values" point in "Constraints and Considerations."

**Q: What happens if I omit the `fieldName` argument in `AddStep<TValue>("MethodName")`?**  
A: A field named `{MethodName}Value` (e.g., `MethodNameValue`) will be automatically generated in the partial class to store the collected input.

**Q: How do I control what happens after all inputs are gathered?**  
A: You always define the final action:
1.  Pass your custom logic as a lambda to the final `.Build(builder => { /* your code here */ })` call.
2.  Alternatively, for a parameterless `.Build()` extension, provide a default factory to `CreateBuilderFor`. If `CreateBuilderFor<YourBuilderType, YourTargetType>(yourDefaultFactory)` is used (where `YourBuilderType` matches the current builder) or `CreateBuilderFor<YourTargetType>(yourDefaultFactory)`, this factory will be invoked by the generated parameterless `Build()` extension.

**Q: Is this only for creating simple data objects?**  
A: No. While excellent for that, its core strength is in structuring the collection of necessary inputs before a user-defined operation. You can use it to configure services, build complex API requests, define data processing pipelines, or any scenario where a clear, guided approach to gathering inputs and initiating an action is beneficial. The "target type" in `CreateBuilderFor<T>` can represent any outcome.

**Q: Does this add any runtime overhead?**  
A: No. All code (interfaces, partial classes, methods) is generated at compile-time. There's no runtime reflection or dynamic invocation related to the generated guided process.

---

## ⚠️ Constraints and Considerations

*   **Class should be partial:** The class annotated with `[StepwiseBuilder]` must be partial.
*   **Parameterless Constructor Required:** `GenerateStepwiseBuilder...CreateBuilderFor<T>()` chain must be defined in a public, parameterless constructor.
*   **Default Value Factories (String-Based):**
    *   The `defaultValueFactory` argument in `AddStep` (and mappers in `AndOverload`, and the default factory in `CreateBuilderFor`) are provided as C# code strings (e.g., `() => true`, `text => int.Parse(text)`).
    *   These strings are embedded directly into the generated code. They can access static members or, if they are instance methods/lambdas using `this` (e.g. `() => this.GetDefaultValue()`), instance members of your builder configuration class.
    *   They **cannot** close over local variables defined within the constructor where `GenerateStepwiseBuilder` is called. Syntax errors or incorrect scopes within these strings will result in compilation errors in the generated file.
*   **Branched Builders and Mandatory Steps:** If a builder uses `.BranchFromStepBefore<BaseBuilder>()`, it must define at least one mandatory (non-default) step via `AddStep<T>(...)` *without* a `defaultValueFactory`. This step is used to generate the extension method that transitions from the base builder into the branched builder.
*   **Default `Build()` Factory and Type Matching:**
    *   When providing a default factory to `CreateBuilderFor<TBuilder, TTarget>(yourFactory)` to enable a parameterless `Build()` extension method, the `TBuilder` generic type argument *must exactly match* the fully qualified name (including generics) of the current builder configuration class being generated.
    *   If you use `CreateBuilderFor<TTarget>(yourFactory)` (single generic argument), this condition is implicitly met.
*   **Initializing Default Values:**
    * The generated Initialize() method on the builder class is responsible for applying any default value factories defined via AddStep(..., defaultValueFactory: ...).
    * The static factory methods in StepwiseBuilders.YourBuilder(...) automatically call Initialize().
    * However, if you instantiate your builder directly using new YourBuilder(...) (for example, when managing dependencies manually or in specific testing scenarios), you must manually call the .Initialize() method on your new builder instance before calling any step methods if you want default values to be applied. .Initialize() returns the interface for the first mandatory step.
---

## 📄 License

Licensed under the MIT License.

---