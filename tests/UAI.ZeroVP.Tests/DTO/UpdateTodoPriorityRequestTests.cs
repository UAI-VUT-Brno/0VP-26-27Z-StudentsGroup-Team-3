using System.ComponentModel.DataAnnotations;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.DTO;

public class UpdateTodoPriorityRequestTests
{
    [Theory]
    [InlineData(TodoPriority.Low)]
    [InlineData(TodoPriority.Normal)]
    [InlineData(TodoPriority.High)]
    public void Validate_ValidRequest_PassesValidation(TodoPriority priority)
    {
        // Arrange
        var request = ValidRequest() with { Priority = priority };

        // Act
        var isValid = Validate(request, out _);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_MissingVersion_FailsValidation()
    {
        // Arrange
        var request = ValidRequest() with { Version = null };

        // Act
        var isValid = Validate(request, out var results);

        // Assert
        Assert.False(isValid);
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(
                nameof(UpdateTodoPriorityRequest.Version)));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Validate_VersionBelowOne_FailsValidation(long version)
    {
        // Arrange
        var request = ValidRequest() with { Version = version };

        // Act
        var isValid = Validate(request, out var results);

        // Assert
        Assert.False(isValid);
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(
                nameof(UpdateTodoPriorityRequest.Version)));
    }

    [Fact]
    public void Validate_InvalidPriority_FailsValidation()
    {
        // Arrange
        var request = ValidRequest() with
        {
            Priority = (TodoPriority)999
        };

        // Act
        var isValid = Validate(request, out var results);

        // Assert
        Assert.False(isValid);
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(
                nameof(UpdateTodoPriorityRequest.Priority)));
    }

    [Fact]
    public void Validate_MissingPriority_FailsValidation()
    {
        // Arrange
        var request = ValidRequest() with { Priority = null };

        // Act
        var isValid = Validate(request, out var results);

        // Assert
        Assert.False(isValid);
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(
                nameof(UpdateTodoPriorityRequest.Priority)));
    }

    private static UpdateTodoPriorityRequest ValidRequest() => new()
    {
        Priority = TodoPriority.High,
        Version = 1
    };

    private static bool Validate(
        UpdateTodoPriorityRequest request,
        out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();

        return Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);
    }
}
