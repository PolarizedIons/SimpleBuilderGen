# SimpleBuilderGen

[![Nuget](https://img.shields.io/nuget/v/PolarizedIons.SimpleBuilderGen?style=for-the-bade)](https://www.nuget.org/packages/PolarizedIons.SimpleBuilderGen/)

Say you have this object:
```c#
[GenerateBuilder]
public class Person {
    public string Name { get; set; }
    public int Age { get; set; }    
}
```

Because you annotated it with `[GenerateBuilder]`, you can now do this:

```c#
var person = new PersonBuilder()
    .WithName("Stephan")
    .WithAge(22)
    .Build();
```

Pretty cool huh?
