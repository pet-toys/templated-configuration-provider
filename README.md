# Templated configuration provider [![test][test-badge]][test-url]

### Features

This library adds its own ConfigurationProvider to the configuration builder.
This provider can override values from previously registered providers.
New values are computed according to the template (if defined).

- Absolute references to values are supported.
- Relative links from the same or parent sections are supported.
- Configuration reload supported (`IOptionMonitor<>`).

### Template examples

Configuration:
```json
{
  "ConnectionStrings": {
    "DbConnection1": "Host=localhost;Password={ConnectionStrings:DbConnection:Password};",
    "DbConnection2": "Host=localhost;Password={DbConnection:Password};",
    "DbConnection:Password": "Pa$Sw0{rD"
  }
}
```

Result:
```json
{
  "ConnectionStrings": {
    "DbConnection1": "Host=localhost;Password=Pa$Sw0{rD;",
    "DbConnection2": "Host=localhost;Password=Pa$Sw0{rD;",
    "DbConnection:Password": "Pa$Sw0{rD"
  }
}
```

More examples in [unit tests](https://github.com/pet-toys/templated-configuration-provider/blob/main/test/PetToys.TemplatedConfigurationProvider.Tests/TemplatedConfigurationProviderTests.cs).

### Getting started

- Provider is installed from NuGet. `dotnet add package PetToys.TemplatedConfigurationProvider`
- Add a using statement to `PetToys.TemplatedConfigurationProvider`
- Add a provider to the configuration builder, preferably by using the  `AddTemplatedConfiguration()` extension method.

### Examples of using

```csharp
using PetToys.TemplatedConfigurationProvider;

/* snip ... */

    IConfigurationRoot configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddUserSecrets(Assembly.GetExecutingAssembly())
        .AddEnvironmentVariables()
        .AddTemplatedConfiguration()
        .AddCommandLine(args)
        .Build();
```

```csharp
using PetToys.TemplatedConfigurationProvider;

/* snip ... */

    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddTemplatedConfiguration(opt =>
    {
        opt.TemplateCharacterStart = '[';
        opt.TemplateCharacterEnd = ']';
    });
```

Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html).

[shields-io]: https://shields.io
[test-badge]: https://github.com/pet-toys/templated-configuration-provider/actions/workflows/test.yml/badge.svg?branch=dev&event=push
[test-url]: https://github.com/pet-toys/templated-configuration-provider/actions/workflows/test.yml
