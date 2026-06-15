# Templated configuration provider

[![Unit Test][test-badge]][test-url] [![NuGet Version][nuget-v-badge]][nuget-url] [![NuGet Downloads][nuget-dt-badge]][nuget-url] [![Target frameworks][dotnet-badge]][nuget-url] [![License][license-badge]][license-url]

> Keep secrets and environment-specific values out of your config files —
> reference them with simple `{placeholders}` and let the provider stitch the
> final values together at runtime.

A drop-in [`IConfigurationProvider`][configuration-provider] for
`Microsoft.Extensions.Configuration`. It sits on top of the configuration
sources you already use (JSON files, environment variables, user secrets, key
vaults, command line, …) and rewrites their values by expanding placeholders
that point at other configuration keys.

## Why

Configuration values often repeat or depend on each other: a connection string
embeds a password that also lives in an environment variable, a base URL is
reused across endpoints, a tenant id appears in half a dozen places. Hard-coding
those values means duplication; splitting them across files means they drift.

This provider lets you keep a single source of truth and reference it everywhere:

- **Keep secrets where they belong.** Store passwords and tokens in environment
  variables, user secrets, or a key vault, and reference them from
  `appsettings.json` without ever committing the secret itself.
- **Don't repeat yourself.** Define a value once and template it into every
  place that needs it.
- **Stay environment-agnostic.** Ship one templated `appsettings.json` and let
  each environment supply the raw values.

It plugs into the standard configuration pipeline, so everything downstream —
`IConfiguration`, the options pattern, and `IOptionsMonitor<T>` — keeps working
unchanged.

## Features

- **Absolute references** to any configuration key.
- **Relative references** resolved against the value's own section and its
  parent sections, falling back to the root.
- **Multiple placeholders** within a single value.
- **Custom delimiters** when the default `{ }` collides with your values.
- **Reload support** — re-templates when an underlying source changes
  (`reloadOnChange` files, `IOptionsMonitor<T>`) and only signals a reload when
  a resolved value actually changes.
- **Case-insensitive** key matching (`OrdinalIgnoreCase`).

## Installation

```sh
dotnet add package PetToys.TemplatedConfigurationProvider
```

## Getting started

Add the provider to your configuration builder with the
`AddTemplatedConfiguration()` extension method. Register it **after** the sources
whose values it should read and override:

```csharp
using PetToys.TemplatedConfigurationProvider;

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .AddTemplatedConfiguration()
    .AddCommandLine(args)
    .Build();
```

For the generic host or minimal APIs:

```csharp
using PetToys.TemplatedConfigurationProvider;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddTemplatedConfiguration();
```

## Usage

### Absolute references

An absolute placeholder is the full configuration key of the value to inject.
This is the classic "secret out of the config file" case:

`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=db;Database=app;Username=app;Password={SECRET:CONNECTIONSTRINGS:POSTGRES:PASSWORD};"
  }
}
```

Environment variable (mapped to the key `SECRET:CONNECTIONSTRINGS:POSTGRES:PASSWORD`):

```sh
SECRET__CONNECTIONSTRINGS__POSTGRES__PASSWORD=Pg$Secr3t
```

Resolved value:

```text
Host=db;Database=app;Username=app;Password=Pg$Secr3t;
```

### Relative references

A placeholder without the full path is resolved relative to the key it lives in:
the provider looks in the value's own section first, then walks up the parent
sections, and finally tries the root (which makes it an absolute reference). This
keeps templates short and refactor-friendly.

```json
{
  "Auth": {
    "Authority": "https://login.example.com/{TenantId}/v2.0/",
    "Authority:TenantId": "5A796309-2459-45E2-9255-FB328599839B"
  }
}
```

`{TenantId}`, referenced from `Auth:Authority`, resolves against
`Auth:Authority:TenantId`:

```text
https://login.example.com/5A796309-2459-45E2-9255-FB328599839B/v2.0/
```

References are scoped to the section hierarchy, so a same-named key under a
*different* section will not satisfy the reference — the placeholder is left
untouched instead.

### Multiple placeholders

Any number of placeholders can appear in one value, and each is resolved
independently:

```json
{
  "Service": {
    "BaseUrl": "https://{Host}:{Port}/api",
    "Host": "localhost",
    "Port": "8080"
  }
}
```

```text
https://localhost:8080/api
```

### Custom delimiters

When `{ }` clashes with literal text in your values, pick a different pair:

```csharp
builder.Configuration.AddTemplatedConfiguration(opt =>
{
    opt.TemplateCharacterStart = '[';
    opt.TemplateCharacterEnd = ']';
});
```

The start and end characters must differ, must not be the configuration key
delimiter (`:`), and must not be whitespace or control characters; otherwise
`AddTemplatedConfiguration` throws an `ArgumentException`.

### Strict mode

By default unresolved placeholders are left untouched. To fail fast when a
balanced placeholder cannot be resolved, enable strict mode:

```csharp
builder.Configuration.AddTemplatedConfiguration(opt =>
{
    opt.ThrowOnUnresolvedPlaceholders = true;
});
```

Strict mode fails fast only during the initial load. On a later reload an
unresolved placeholder is not thrown from the change callback; the provider
keeps the previous resolved values and does not raise a reload notification.

### Reload support

When the provider sits on top of a reloadable source (for example a JSON file
added with `reloadOnChange: true`), it re-evaluates the templates on every
change and refreshes the resolved values. A reload notification is raised only
when a resolved value actually changes, so `IOptionsMonitor<T>` consumers are
not woken up for no-op reloads.

## Good to know

- **Substitution is single-pass.** Placeholders are expanded against the *raw*
  source values, not recursively. If a referenced value itself contains a
  placeholder, it is inserted verbatim rather than expanded again.
- **Unresolved placeholders pass through untouched by default.** A balanced
  placeholder whose key cannot be resolved is left in the value as-is unless
  strict mode is enabled. Unbalanced delimiters are always left unchanged.
- **Order matters.** The provider overrides values from the sources registered
  before it. Place it after those sources, and after it any source that should
  win over the templated result (such as command-line arguments).

More runnable examples live in the [unit tests][tests-url].

## License

Provided under the [Apache License, Version 2.0][license-url].

[test-badge]: https://img.shields.io/github/actions/workflow/status/pet-toys/templated-configuration-provider/test.yml?branch=dev&style=flat-square&logo=github&label=test
[test-url]: https://github.com/pet-toys/templated-configuration-provider/actions?query=workflow%3Atest+branch%3Adev
[nuget-v-badge]: https://img.shields.io/nuget/v/PetToys.TemplatedConfigurationProvider?style=flat-square&logo=nuget&label=version
[nuget-dt-badge]: https://img.shields.io/nuget/dt/PetToys.TemplatedConfigurationProvider?style=flat-square&logo=nuget
[nuget-url]: https://www.nuget.org/packages/PetToys.TemplatedConfigurationProvider/
[license-badge]: https://img.shields.io/github/license/pet-toys/templated-configuration-provider?style=flat-square&color=blue
[license-url]: https://www.apache.org/licenses/LICENSE-2.0
[dotnet-badge]: https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4?style=flat-square&logo=dotnet
[configuration-provider]: https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.iconfigurationprovider
[tests-url]: https://github.com/pet-toys/templated-configuration-provider/tree/dev/test/PetToys.TemplatedConfigurationProvider.Tests
