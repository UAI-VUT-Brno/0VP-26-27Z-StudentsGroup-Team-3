
# API Performance Profiling – OVP-T3-03

## Objective

Verify the response times of all Todo API endpoints against the
required maximum response time of 500 ms.

## Test methodology

- Framework: xUnit / ASP.NET Core WebApplicationFactory
- Runtime: .NET 10
- Server: ASP.NET Core TestServer (in-process)
- Data storage: In-memory repository
- Warm-up: 10 requests per endpoint
- Measurement: 50 requests per endpoint
- Total: 300 measured requests
- Statistics: Average, Minimum, Maximum, P50, P95
- Time measurement: Stopwatch around the HTTP request
- Automatic validation: Measured maximum must be below 500 ms

Setup operations for PUT, Complete and DELETE are performed
outside the measured interval.

## Benchmark results

Date: 2026-10-12

| Endpoint | Average | P95 | Maximum |
|---|---:|---:|---:|
| GET /api/todos | 5.40 ms | 6.25 ms | 6.75 ms |
| GET /api/todos/{id} | 4.62 ms | 5.41 ms | 5.53 ms |
| POST /api/todos | 5.94 ms | 6.77 ms | 8.02 ms |
| PUT /api/todos/{id} | 4.63 ms | 5.82 ms | 6.02 ms |
| POST /api/todos/{id}/complete | 5.67 ms | 6.52 ms | 7.19 ms |
| DELETE /api/todos/{id} | 5.75 ms | 6.56 ms | 6.91 ms |

All 300 measured requests completed below 500 ms.

The highest observed response time was 8.02 ms.

## Test execution

Run performance tests:

```powershell
dotnet test UAI.ZeroVP.sln -c Release --filter "FullyQualifiedName~HttpPerformanceTests" --logger "console;verbosity=detailed"
```

Run all tests:

```powershell
dotnet test UAI.ZeroVP.sln -c Release
```

Full test suite result: 28 passed, 0 failed, 0 skipped.

## Limitations

The measurements use an in-process TestServer rather than
a real network connection.

Requests are executed sequentially without concurrent load.

The application uses an in-memory repository instead of a
production database.

Duplicate application log entries were observed during testing.
Their cause has not yet been verified.

Consequently, these results establish a local performance baseline,
not a guarantee of response times under production load.

## Conclusion

All six Todo API endpoints met the 500 ms response-time target
in the measured local scenarios.

No application-level optimization was required based on these
measurements.

Concurrent load and real HTTP-server performance will be
evaluated separately on branch feature/api-load-testing.
