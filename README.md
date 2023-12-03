# DuckInterface

This repository contains my attempt to enable duck typing support in C#. It is powered by Roslyn and the new C# 9 feature the Source Generators. 
I would say it is purely academic/just for fun stuff, but for some scenarios, it can be useful.

[![Nuget](https://img.shields.io/badge/nuget-DuckInterface-blue?style=flat-square&logo=nuget)](https://www.nuget.org/packages/DuckInterface/)

# How to use it

Let's suppose that you have the next declaration:

``` cs 
public interface ICalculator
{
  float Calculate(float a, float b);
}

public class AddCalculator
{

  public float Calculate(float a, float b);
}
```

It is important to notice that the `` AddCalculator `` doesn't implement the `` ICalculator `` in any way. It just has an identical method declaration.
If we try to use it like in the next snippet, we will get a compilation error:

``` cs
var addCalculator = new AddCalculator();

var result = ApplyCalculator(addCalculator, 10, 20);

float ApplyCalculator(ICalculator calculator, float a, float b)
{
  return calculator.Calculate(a, b);
}

```
In this case, duck typing can be helpful because it will allow us to pass `` AddCalculator easily ``. The `` DuckInterface `` may help with it. 
You will need to install the NuGet package and call the `` Duck<> `` extension method where we are passing the variable:

``` cs 
var calculator = new AddCalculator();
var result = ApplyCalculator(calculator.Duck<ICalculator>(), 10, 20);

Console.WriteLine($"Result: {result}");

static float ApplyCalculator(ICalculator calculator, float a, float b)
{
    return calculator.Calculate(a, b);
}
```

And it's done. The compilation errors are gone, and everything works as expected.

# How it works

There is a source generator that looks for a method call and variable assignments to understand how the duckable interface may be used. 
For example, let's look for the next snippet:
``` cs
var result = ApplyCalculator(addCalculator.Duck<ICalculator>(), 10, 20);
``` 

The analyzer will see that the ``` Duck ``` extension method is utilized, and then it will check the type of ``` addCalculator ``` variable.
If the type has all the required members, the source generator will generate a container and new extension method with concrete type:
``` cs
public static class Duck_expDuckable_ICalculator_expDuckable_AddCalculator_Extensions
{
    public static TInterface Duck<TInterface>(this global::expDuckable.AddCalculator value) 
        where TInterface: class
    {
        return new Duck_expDuckable_ICalculator(value) as TInterface;
    }
}

public static class DuckHandlerExtensionsForexpDuckable_ICalculator
{

    public static global::expDuckable.ICalculator Create(this IDuckHandler<global::expDuckable.ICalculator> handler, Func<float, float, float> calculate)
    {
        return new DuckInterface.Generated.expDuckable.Duck_expDuckable_ICalculator(calculate);
    }

    public static global::expDuckable.ICalculator CreatePartial(this IDuckHandler<global::expDuckable.ICalculator> handler, Func<float, float, float> calculate = default)
    {
        return new DuckInterface.Generated.expDuckable.Duck_expDuckable_ICalculator(calculate);
    }


    public static global::expDuckable.ICalculator Make(this IDuckHandler<global::expDuckable.ICalculator> handler, Func<float, float, float> calculate)
    {
        return new DuckInterface.Generated.expDuckable.Duck_expDuckable_ICalculator(calculate);
    }

    public static global::expDuckable.ICalculator MakePartial(this IDuckHandler<global::expDuckable.ICalculator> handler, Func<float, float, float> calculate = default)
    {
        return new DuckInterface.Generated.expDuckable.Duck_expDuckable_ICalculator(calculate);
    }
}

[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public partial class Duck_expDuckable_ICalculator: global::expDuckable.ICalculator 
{
    public Duck_expDuckable_ICalculator(Func<float, float, float> calculate)
    {
        _Calculate = calculate;
    }

    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)] 
    private readonly Func<float, float, float> _Calculate;        


    [System.Diagnostics.DebuggerStepThrough]
    public float Calculate(float a, float b)
    {
        return _Calculate(a, b);
    }
}

```
