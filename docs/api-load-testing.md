# API Load Testing – OVP-T3-03

## Objective

Verify the 500 ms response-time requirement for all six Todo API endpoints under concurrent load and identify potential performance bottlenecks.

## Test Methodology

- **Framework:** xUnit, ASP.NET Core WebApplicationFactory (TestServer)
- **Runtime:** .NET 10, Release
- **Storage:** In-memory repository
- **Concurrency:** 10, 50 and 100 requests
- **Execution:** 10 warm-up requests + 3 measured rounds per scenario
- **Measurements:** Average, P95, P99, Maximum, HTTP errors
- **Total:** 18 scenarios, 2,880 measured HTTP requests

Requests are started using a shared `TaskCompletionSource` signal. Required Todo records are prepared before measurement.

**Test implementation:** `tests/UAI.ZeroVP.Tests/Performance/ApiLoadTests.cs`

## Results

Maximum response times at 100 concurrent requests:

Date: 2026-10-12

**All response times are in milliseconds.**

| Endpoint | Concurrency | Average | P95 | P99 | Maximum |
|---|---:|---:|---:|---:|---:|
| GET /api/todos | 10 | 0.55 | 0.80 | 0.83 | 0.83 |
| GET /api/todos | 50 | 1.35 | 2.23 | 2.39 | 2.53 |
| GET /api/todos | 100 | 2.43 | 12.41 | 13.56 | 13.94 |
| GET /api/todos/{id} | 10 | 0.62 | 0.86 | 0.96 | 0.96 |
| GET /api/todos/{id} | 50 | 0.89 | 1.67 | 2.10 | 2.16 |
| GET /api/todos/{id} | 100 | 2.12 | 6.02 | 6.47 | 7.02 |
| POST /api/todos | 10 | 1.23 | 1.64 | 1.91 | 1.91 |
| POST /api/todos | 50 | 2.54 | 5.33 | 5.77 | 5.82 |
| POST /api/todos | 100 | 3.73 | 7.80 | 8.46 | 11.36 |
| PUT /api/todos/{id} | 10 | 0.73 | 0.99 | 1.02 | 1.02 |
| PUT /api/todos/{id} | 50 | 1.12 | 1.39 | 7.65 | 7.69 |
| PUT /api/todos/{id} | 100 | 1.68 | 5.65 | 7.51 | 8.58 |
| POST /api/todos/{id}/complete | 10 | 0.87 | 4.09 | 4.31 | 4.31 |
| POST /api/todos/{id}/complete | 50 | 1.74 | 4.02 | 4.38 | 4.42 |
| POST /api/todos/{id}/complete | 100 | 3.94 | 10.70 | 14.97 | 17.38 |
| DELETE /api/todos/{id} | 10 | 0.91 | 1.52 | 1.70 | 1.70 |
| DELETE /api/todos/{id} | 50 | 2.57 | 4.52 | 4.64 | 4.72 |
| DELETE /api/todos/{id} | 100 | 4.93 | 9.27 | 10.13 | 13.02 |

**Max values per category in miliseconds.**

| Endpoint | Maximum |
|---|---:|
| GET /api/todos | 13.94 ms |
| GET /api/todos/{id} | 7.02 ms |
| POST /api/todos | 11.36 ms |
| PUT /api/todos/{id} | 8.58 ms |
| POST /api/todos/{id}/complete | 17.38 ms |
| DELETE /api/todos/{id} | 13.02 ms |

**Summary:**
- 18/18 scenarios passed
- 2,880/2,880 expected HTTP responses
- 0 HTTP or transport errors
- 0 responses ≥ 500 ms
- Highest response time: **17.38 ms**

## Performance Optimization

Initial tests revealed significant overhead caused by Serilog logging to the network-based project directory (GIGADISK).

Changes in `src/UAI.ZeroVP.API/Program.cs`:

- Restricted Microsoft framework logs to `Warning`.
- Preserved application `Information` logs and HTTP request logging.
- Moved default log storage to a local application-data directory.
- Disabled console logging outside Development.
- Added configurable log-file path (`Serilog:FilePath`).

No CRUD algorithm changes were required.

## Verification

Run load tests:

```powershell
dotnet test UAI.ZeroVP.sln -c Release --filter "FullyQualifiedName~ApiLoadTests"
```

Run all tests:

```powershell
dotnet test UAI.ZeroVP.sln -c Release
```

**Final result: 46/46 tests passed, 0 failed.**

## Conclusion

All tested API endpoints met the **500 ms response-time requirement**, including scenarios with 100 concurrent requests.

The main performance bottleneck was associated with logging to network storage. Optimizing Serilog configuration significantly improved response times.

**Limitation:** Measurements use an in-process TestServer and an in-memory repository, not a deployed Kestrel server. The 500 ms limit is reported diagnostically in load tests; exceeding it alone does not fail the test.
