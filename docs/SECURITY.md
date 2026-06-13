# Security Policy

## Supported versions

The package major version tracks the latest supported .NET major version. Only
the latest major line receives security fixes.

| Version | Supported          |
| ------- | :----------------: |
| 10.x    | :white_check_mark: |
| 8.x     | :x:                |
| < 8.0   | :x:                |

## Reporting a vulnerability

Please do not report security vulnerabilities through public issues, pull
requests, or discussions.

Instead, use GitHub's private vulnerability reporting: open the repository's
**Security** tab and click **Report a vulnerability**. This keeps the report
confidential until a fix is available.

When reporting, please include as much of the following as you can:

- A description of the vulnerability and its impact.
- The affected package version(s) and target framework.
- Steps to reproduce, ideally with a minimal configuration sample.
- Any known workarounds or mitigations.

## What to expect

- We aim to acknowledge a report within a few days.
- We will keep you informed as we investigate and work on a fix.
- Once a fix ships, we will publish a security advisory and credit the
  reporter, unless you prefer to remain anonymous.

## Scope

This library transforms configuration values by substituting placeholders with
values resolved from other configuration sources. It does not store secrets,
perform network calls, or grant access to anything on its own. Reports about how
the substitution logic could expose or mishandle configured values are in scope;
issues caused solely by how a consuming application sources or protects its own
configuration are not.
