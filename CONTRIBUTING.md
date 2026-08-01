# Contributing

## Prerequisites

- .NET SDK `10.0.302` or a compatible allowed patch
- Git
- Docker Desktop for container and later integration-test workflows

## Local validation

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/test-results
dotnet tool restore
dotnet tool run reportgenerator -- "-reports:artifacts/test-results/**/coverage.cobertura.xml" "-targetdir:artifacts/coverage" "-assemblyfilters:+WorkOps.*;-WorkOps.*Tests" "-reporttypes:Cobertura;TextSummary"
./scripts/check-coverage.sh artifacts/coverage/Cobertura.xml 70 35
docker compose config
```

## Change expectations

Create a focused branch and pull request. Explain motivation, design, security and privacy impact,
tests, and relevant operational effects. Keep commits intentional and do not manufacture activity.

Respect the dependency rules in [architecture](docs/architecture.md). New request or response types
must be explicit contracts. Validate and bound all external input, prevent over-posting, and define
a deliberate sanitization or no-mutation policy for each new input surface.

Database changes must include a migration, an empty-database test, an upgrade-path test once a prior
release exists, and rollback/compatibility notes.

Contributions must not contain third-party proprietary code, data, documents, identifiers, or
credentials. Use independently written code and synthetic data only.

## Pull-request checklist

- [ ] Scope and tradeoffs are described
- [ ] External inputs have validation and sanitization policies
- [ ] Tenant and authorization impact is covered
- [ ] Tests cover the meaningful behavior and failure path
- [ ] Logs and errors contain no sensitive values
- [ ] Documentation and ADRs are updated when behavior or decisions change
- [ ] Line and branch coverage remain above the repository floors
- [ ] Restore, format, build, tests, dependency audit, secret scan, and container scan pass
