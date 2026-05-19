## Summary


## Related Issue

Closes #
<!-- Use References # instead when this is related or partial work that should not auto-close the issue. -->

## Validation

<!-- Mark any validation that is not applicable as N/A in the PR body. -->
- [ ] `dotnet restore Memora.sln`
- [ ] `dotnet build Memora.sln --no-restore`
- [ ] `dotnet test Memora.sln --no-build --nologo`

## Risk Checklist

- [ ] This PR does not change CI/CD, GitHub Actions, Dockerfiles, install scripts, or post-install hooks.
- [ ] This PR does not add telemetry, analytics, network calls, or dependency updates.
- [ ] This PR does not change auth, token handling, secret handling, MCP execution, or shell execution.
- [ ] Any checked item above that is not true is called out in the summary and ready for maintainer review.
