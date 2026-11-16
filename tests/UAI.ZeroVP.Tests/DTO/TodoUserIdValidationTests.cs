using System.ComponentModel.DataAnnotations;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.DTO;

public class TodoUserIdValidationTests
{
    [Fact]
    public void CreateTodoItemRequest_UserIdOmitted_PassesValidation()
    {
        // Arrange
        var request = new CreateTodoItemRequest
        {
            Title = "Valid task"
        };

        // Act
        var isValid = Validate(request, out var results);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void CreateTodoItemRequest_UserIdAtMaxLength_PassesValidation()
    {
        // Arrange
        var request = new CreateTodoItemRequest
        {
            Title = "Valid task",
            UserId = new string('a', 100)
        };

        // Act
        var isValid = Validate(request, out var results);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void CreateTodoItemRequest_UserIdTooLong_FailsValidation()
    {
        // Arrange
        var request = new CreateTodoItemRequest
        {
            Title = "Valid task",
            UserId = new string('a', 101)
        };

        // Act
        var isValid = Validate(request, out var results);

        // Assert
        Assert.False(isValid);
        var result = Assert.Single(results);
        Assert.Contains(nameof(CreateTodoItemRequest.UserId), result.MemberNames);
        Assert.Equal("User ID cannot exceed 100 characters.", result.ErrorMessage);
    }

    [Fact]
    public void UpdateTodoItemRequest_UserIdTooLong_FailsValidation()
    {
        // Arrange
        var request = new UpdateTodoItemRequest
        {
            Title = "Valid task",
            Version = 1,
            UserId = new string('a', 101)
        };

        // Act
        var isValid = Validate(request, out var results);

        // Assert
        Assert.False(isValid);
        var result = Assert.Single(results);
        Assert.Contains(nameof(UpdateTodoItemRequest.UserId), result.MemberNames);
        Assert.Equal("User ID cannot exceed 100 characters.", result.ErrorMessage);
    }

    private static bool Validate(object request, out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();

        return Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);
    }
}
