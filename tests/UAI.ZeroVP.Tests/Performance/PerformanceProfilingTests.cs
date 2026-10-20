using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using UAI.ZeroVP.API.Controllers;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;
using Xunit;

namespace UAI.ZeroVP.Tests;

public class PerformanceProfilingTests
{
    private readonly TodosController _todosController;
    private readonly TimeProvider _timeProvider;

    public PerformanceProfilingTests()
    {
        _timeProvider = TimeProvider.System;
        var repository = new InMemoryTodoRepository(
            new InMemoryTodoOptions
            {
                SeedSampleData = false
            });
        var logger = NullLogger<TodosController>.Instance;
        _todosController = new TodosController(repository, _timeProvider, logger);
    }

    [Fact]
    public async Task ApiEndpoints_ResponseTime_ShouldBeWellUnder500Milliseconds()
    {
        var ct = CancellationToken.None;
        var stopwatch = Stopwatch.StartNew();

        // 1. Measuring task creation
        var createRequest = new CreateTodoItemRequest
        {
            Title = "Profilovací úkol",
            Priority = TodoPriority.High
        };
        var createResult = await _todosController.Create(createRequest, ct);
        var created = Assert.IsType<CreatedAtRouteResult>(createResult.Result);
        var createdDto = Assert.IsType<TodoItemDto>(created.Value);

        // 2. Measuring detail query
        var getResult = await _todosController.GetById(createdDto.Id, ct);
        Assert.IsType<OkObjectResult>(getResult.Result);

        // 3. Measuring retrieval of entire list
        var listResult = await _todosController.GetAll(null, ct);
        Assert.IsType<OkObjectResult>(listResult.Result);

        // 4. Measuring deletion of task
        var deleteResult = await _todosController.Delete(createdDto.Id, ct);
        Assert.IsType<NoContentResult>(deleteResult);

        stopwatch.Stop();

        // Verification of limit compliance
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Response time exceeded 500 ms: measured {stopwatch.ElapsedMilliseconds} ms");
    }
}
