using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StepwiseBuilderGenerator.Sample;

// 1) Single overload on the very first step
[StepwiseBuilder]
public partial class SimpleOverloadExample
{
    public SimpleOverloadExample()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetId")
            .AndOverload<string, int>(s => int.Parse(s))
            .AddStep<string>("SetName")
            .CreateBuilderFor<string>();
    }
}

// 2) Multiple overloads on a single step
[StepwiseBuilder]
public partial class MultipleOverloadsExample
{
    public MultipleOverloadsExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("ProcessData")
            .AndOverload<int, string>(i => i.ToString())
            .AndOverload<DateTime, string>(dt => dt.ToShortDateString())
            .AndOverload<Guid, string>(g => g.ToString("N"))
            .CreateBuilderFor<bool>();
    }
}

// 3) Overloads on different steps in the same chain
[StepwiseBuilder]
public partial class OverloadsOnDifferentStepsExample
{
    public OverloadsOnDifferentStepsExample()
    {
        GenerateStepwiseBuilder
            .AddStep<double>("SetValue")
            .AndOverload<string, double>(s => double.Parse(s))
            .AddStep<int>("SetCount")
            .AndOverload<string, int>(s => int.Parse(s))
            .AddStep<bool>("EnableFlag")
            .AndOverload<int, bool>(i => i != 0)
            .CreateBuilderFor<string>();
    }
}

// 4) Generic builder with an overload mapping Func<T,T> → T
[StepwiseBuilder]
public partial class GenericOverloadExample<T>
    where T : new()
{
    public GenericOverloadExample()
    {
        GenerateStepwiseBuilder
            .AddStep<T>("Configure")
            .AndOverload<Func<T, T>, T>(f => f(new T()))
            .AddStep<string>("Summary")
            .CreateBuilderFor<T>();
    }
}

// 5) Method-group overloads and enum parsing
[StepwiseBuilder]
public partial class MethodGroupEnumOverloadExample
{
    public MethodGroupEnumOverloadExample()
    {
        GenerateStepwiseBuilder
            .AddStep<DateTime>("SetTimestamp")
            // built-in static Parse methods
            .AndOverload<string, DateTime>(DateTime.Parse)
            .AndOverload<long, DateTime>(ms => DateTimeOffset.FromUnixTimeMilliseconds(ms).DateTime)
            .AddStep<DayOfWeek>("SetDayOfWeek")
            // generic Enum.Parse<T>
            .AndOverload<string, DayOfWeek>(Enum.Parse<DayOfWeek>)
            .CreateBuilderFor<Schedule>();
    }
}

// 6) Collection and string-to-sequence mapping
[StepwiseBuilder]
public partial class CollectionOverloadExample
{
    public CollectionOverloadExample()
    {
        GenerateStepwiseBuilder
            .AddStep<IEnumerable<int>>("SetNumbers")
            // array → IEnumerable<int>
            .AndOverload<int[], IEnumerable<int>>(arr => arr)
            .AddStep<int>("SetThreshold")
            .CreateBuilderFor<Stats>();
    }
}

// 7) Custom domain types and parse helpers
[StepwiseBuilder]
public partial class MethodPassedAsOverloadExample
{
    public MethodPassedAsOverloadExample()
    {
        GenerateStepwiseBuilder
            .AddStep<Address>("SetAddress")
            // pass method
            .AndOverload<string, Address>(Address.Parse)
            .CreateBuilderFor<CustomerProfile>();
    }
}

// 8) Async call
[StepwiseBuilder]
public partial class OverloadWithAsyncCallExample
{
    public OverloadWithAsyncCallExample()
    {
        GenerateStepwiseBuilder
            .AddStep<Task<Address>>("SetAddress", defaultValueFactory: async () => await Task.FromResult(new Address()))
            // pass async method
            .AndOverload<string, Task<Address>>(async s => await Task.Run(() => Address.Parse(s)))
            .CreateBuilderFor<CustomerProfile>();
    }
}

// 9) Branch with overloads
[StepwiseBuilder]
public partial class OverloadsInBranchCallExample
{
    public OverloadsInBranchCallExample()
    {
        GenerateStepwiseBuilder
            .BranchFrom<SimpleOverloadExample>("SetName")
            .AddStep<string>("SetAddress")
            .AndOverload<int, string>(_ => "42")
            .AddStep<string>("SetAddress1")
            .CreateBuilderFor<string>();
    }

    public void Test()
    {
        // SetAddress with int parameter is accessible 
        //StepwiseBuilders.SimpleOverloadExample().SetId(123).SetAddress(123).SetAddress1("").Build(default);
    }
}

// 10) Overload with custom name
[StepwiseBuilder]
public partial class OverloadWithNewNameExample
{
    public OverloadWithNewNameExample()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetId")
            .AndOverload<string, int>(s => int.Parse(s), "SetIdFromString")
            .AddStep<string>("SetName")
            .CreateBuilderFor<string>();
    }
    public void Test()
    {
        //StepwiseBuilders.OverloadWithNewNameExample().SetIdFromString("123");
    }
}

// Dummy domain types used above
public class Schedule
{
    /* ... */
}

public class Stats
{
    /* ... */
}

public class Address
{
    public static Address Parse(string s) => /*...*/ throw null;
}

public class CustomerProfile
{
    /* ... */
}