using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using UAI.ZeroVP.API.Controllers;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.Controllers;

public class TodosControllerTests
{
    private static TodosController CreateController()
    {
        var repository = new InMemoryTodoRepository(
            new InMemoryTodoOptions
            {
                SeedSampleData = false
            });

        return new TodosController(
            repository,
            TimeProvider.System,
            NullLogger<TodosController>.Instance);
    }

    [Fact]
    public async Task GetAll_EmptyRepository_ReturnsOkWithEmptyList()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetAll(
            isCompleted: null,
            cancellationToken: CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<TodoItemDto>>(okResult.Value);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedTodo()
    {
        // Arrange
        var controller = CreateController();

        var request = new CreateTodoItemRequest
        {
            Title = "  New task  ",
            Description = "  Test description  ",
            Priority = TodoPriority.High
        };

        // Act
        var result = await controller.Create(
            request,
            CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtRouteResult>(result.Result);
        var todo = Assert.IsType<TodoItemDto>(createdResult.Value);

        Assert.Equal(nameof(TodosController.GetById), createdResult.RouteName);
        Assert.NotEqual(Guid.Empty, todo.Id);
        Assert.Equal("New task", todo.Title);
        Assert.Equal("Test description", todo.Description);
        Assert.Equal(TodoPriority.High, todo.Priority);
        Assert.False(todo.IsCompleted);
        Assert.Null(todo.CompletedAtUtc);
        Assert.Null(todo.DueAtUtc);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateController();
        var id = Guid.NewGuid();

        // Act
        var result = await controller.GetById(
            id,
            CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);

        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingTodo_ReturnsNoContent()
    {
        // Arrange
        var repository = new InMemoryTodoRepository(
            new InMemoryTodoOptions
            {
                SeedSampleData = false
            });

        var controller = new TodosController(
            repository,
            TimeProvider.System,
            NullLogger<TodosController>.Instance);

        var item = new UAI.ZeroVP.Data.Entities.TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Delete me",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(item);

        // Act
        var result = await controller.Delete(
            item.Id,
            CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repository.GetByIdAsync(item.Id));
    }

    [Fact]
    public async Task Delete_UnknownTodo_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateController();
        var id = Guid.NewGuid();

        // Act
        var result = await controller.Delete(
            id,
            CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);

        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task Complete_ExistingTodo_ReturnsCompletedTodo()
    {
        // Arrange
        var repository = new InMemoryTodoRepository(
            new InMemoryTodoOptions
            {
                SeedSampleData = false
            });

        var controller = new TodosController(
            repository,
            TimeProvider.System,
            NullLogger<TodosController>.Instance);

        var item = new UAI.ZeroVP.Data.Entities.TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Complete me",
            Priority = TodoPriority.Normal,
            IsCompleted = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(item);

        // Act
        var result = await controller.Complete(
            item.Id,
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var todo = Assert.IsType<TodoItemDto>(okResult.Value);

        Assert.True(todo.IsCompleted);
        Assert.NotNull(todo.CompletedAtUtc);
    }

    [Fact]
    public async Task Complete_UnknownTodo_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateController();
        var id = Guid.NewGuid();

        // Act
        var result = await controller.Complete(
            id,
            CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);

        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }
}