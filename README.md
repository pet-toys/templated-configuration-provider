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
- **Inline default values** — `{Db:Host:-localhost}` falls back to a literal
  when the key supplies nothing (opt-in).
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
the provider tries the root first, then prefixes the placeholder with each
section of the value's own key in turn — from the outermost section down to the
value's own — and takes the first match. This keeps templates short and
refactor-friendly.

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

Because the root is tried first, a root-level key of the same name **wins** over
a nearer, section-scoped one. If both `TenantId` and `Auth:Authority:TenantId`
exist, `{TenantId}` in `Auth:Authority` resolves to the root value. Use the full
path when you need the nearer key regardless of what sits at the root.

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

### Inline default values

A placeholder can carry its own fallback, so an optional key does not have to
exist in every environment. The syntax is off until you name the separator that
splits the key from the default — `":-"` is the conventional choice:

```csharp
builder.Configuration.AddTemplatedConfiguration(opt =>
{
    opt.DefaultValueSeparator = ":-";
});
```

```jsonc
{
  "Db": {
    "Connection": "Server={Db:Host:-localhost};Database=app"
  }
}
```

With no `Db:Host` in the configuration, `Db:Connection` resolves to
`Server=localhost;Database=app`; add `Db:Host` and its value wins.

- The **first** occurrence of the separator splits the placeholder, so the
  default itself may contain the separator.
- The default is used when the key resolves to **nothing** — absent, null or
  empty. Without a default, a key that resolves to an empty string still
  substitutes an empty string; naming a default says that empty is not the
  answer you want.
- The default is **literal text**: an empty default (`{Db:Host:-}`) erases the
  placeholder, and delimiters inside a default are not resolved further.
- A placeholder carrying a default always resolves, so it never trips strict
  mode.
- The default **cannot contain the end delimiter**, which closes the
  placeholder. Custom delimiters are the way out.

When the separator is left unset (the default), a placeholder is read as a
configuration key in full, exactly as it was before the option existed. It must
not be empty or whitespace, must not contain either template delimiter, and must
not be the bare configuration key delimiter (`:`); otherwise
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

A placeholder that carries an inline default is resolved by definition and never
throws. Strict mode fails fast only during the initial load. On a later reload an
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
  placeholder whose key cannot be resolved is left in the value as-is unless it
  carries an inline default or strict mode is enabled. Unbalanced delimiters are
  always left unchanged.
- **Order matters.** The provider overrides values from the sources registered
  before it. Place it after those sources, and after it any source that should
  win over the templated result (such as command-line arguments).

More runnable examples live in the [unit tests][tests-url].

## Limitations

- **Every source it reads is built a second time.** To resolve placeholders the
  provider assembles its own configuration root from the sources registered
  before it, so those sources are built twice for the lifetime of the
  application: once by the outer root and once inside the provider. For a JSON
  file with `reloadOnChange: true` that means a second file watcher, and for a
  source with side effects — a remote store, a secrets vault — it means the
  fetch happens twice.
- **Two templated providers do not compose.** When it builds its inner root the
  provider skips every `TemplatedConfigurationSource`, its own included, so a
  second templated provider cannot see the values a first one resolved.
  Registering more than one gives you two independent providers over the same
  untemplated sources, not a chain.

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
