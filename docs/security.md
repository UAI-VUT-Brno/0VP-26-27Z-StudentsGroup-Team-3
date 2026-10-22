# Security & Vulnerability Scanning

This project integrates automated dependency security scanning into both the local development workflow and the continuous integration pipeline (GitHub Actions) to adhere to DevSecOps practices.

## Automated CI Pipeline Gate

In `.github/workflows/ci.yml`, an audit step runs directly after dependency restoration:

```bash
dotnet list UAI.ZeroVP.sln package --vulnerable --include-transitive