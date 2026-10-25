
# Security & Vulnerability Scanning

The project uses NuGet Audit to identify known security
vulnerabilities in its .NET dependencies.

Security checks are available for local development and
are enforced by the GitHub Actions CI pipeline.

## Automated CI Security Gate

The workflow is defined in:

`.github/workflows/ci.yml`

During dependency restoration, NuGet Audit checks both
direct and transitive package dependencies.

The CI security gate uses:

- `NuGetAudit=true` to enable vulnerability auditing.
- `NuGetAuditMode=all` to include transitive dependencies.
- `NuGetAuditLevel=low` to include all vulnerability severities.
- `NU1901` to `NU1904` as errors for known vulnerabilities.
- `NU1900` and `NU1905` as errors for audit source problems.

When any of these conditions occurs, dependency restoration
fails and subsequent CI steps are not executed.

## Local Security Audit

Run the audit from the repository root:

```powershell
dotnet restore UAI.ZeroVP.sln `
    -p:NuGetAudit=true `
    -p:NuGetAuditMode=all `
    -p:NuGetAuditLevel=low `
    -warnaserror:NU1900,NU1901,NU1902,NU1903,NU1904,NU1905
```

A failed command indicates either a detected vulnerability
or a problem accessing the vulnerability audit data.

To list vulnerable packages without enforcing the CI gate:

```powershell
dotnet list UAI.ZeroVP.sln package --vulnerable --include-transitive
```

The package list command is useful for investigation,
but the restore command is used as the enforced CI gate.

## Vulnerability Severity

| Warning | Meaning |
|---|---|
| NU1901 | Low severity vulnerability |
| NU1902 | Moderate severity vulnerability |
| NU1903 | High severity vulnerability |
| NU1904 | Critical severity vulnerability |
| NU1900 | Vulnerability audit source communication failure |
| NU1905 | Audit source does not provide vulnerability information |

## Handling Detected Vulnerabilities

When the audit fails:

1. Identify the affected NuGet package and vulnerability.
2. Check whether the dependency is direct or transitive.
3. Review the corresponding security advisory.
4. Update or replace the vulnerable dependency.
5. Repeat the security audit and automated tests.

Known vulnerabilities should not be silently ignored.

## Scope and Limitations

NuGet Audit checks known vulnerabilities in .NET packages.

It does not replace static code analysis, application
security testing, or manual security review.

The audit results depend on the availability and
currency of the configured vulnerability data sources.
