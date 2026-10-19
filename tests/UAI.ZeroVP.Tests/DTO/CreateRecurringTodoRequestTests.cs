
using System.ComponentModel.DataAnnotations;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Tests.DTO;

public class CreateRecurringTodoRequestTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(53)]
    [InlineData(100)]
    public void RepeatCount_OutOfRange_FailsValidation(int repeatCount)
    {
        var request = ValidRequest() with { RepeatCount = repeatCount };

        Assert.False(Validate(request));
    }

    [Fact]
    public void MissingFirstDueDate_FailsValidation()
    {
        var request = ValidRequest() with { FirstDueAtUtc = null };

        Assert.False(Validate(request));
    }

    [Fact]
    public void NonUtcFirstDueDate_FailsValidation()
    {
        var request = ValidRequest() with
        {
            FirstDueAtUtc = new DateTimeOffset(
                2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(2))
        };

        Assert.False(Validate(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidTitle_FailsValidation(string title)
    {
        var request = ValidRequest() with { Title = title };

        Assert.False(Validate(request));
    }

    [Fact]
    public void TooLongDescription_FailsValidation()
    {
        var request = ValidRequest() with
        {
            Description = new string('A', 1001)
        };

        Assert.False(Validate(request));
    }

    [Fact]
    public void InvalidPriority_FailsValidation()
    {
        var request = ValidRequest() with
        {
            Priority = (TodoPriority)999
        };

        Assert.False(Validate(request));
    }

    [Fact]
    public void DateOverflow_FailsValidation()
    {
        var request = ValidRequest() with
        {
            FirstDueAtUtc = DateTimeOffset.MaxValue,
            RepeatCount = 52
        };

        Assert.False(Validate(request));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(52)]
    public void ValidRequest_PassesValidation(int repeatCount)
    {
        var request = ValidRequest() with { RepeatCount = repeatCount };

        Assert.True(Validate(request));
    }

    private static CreateRecurringTodoRequest ValidRequest() => new()
    {
        Title = "Weekly meeting",
        Description = "Team sync",
        Priority = TodoPriority.Normal,
        FirstDueAtUtc = new DateTimeOffset(
            2026, 10, 20, 10, 0, 0, TimeSpan.Zero),
        RepeatCount = 3
    };

    private static bool Validate(CreateRecurringTodoRequest request)
    {
        var results = new List<ValidationResult>();

        return Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);
    }
}
