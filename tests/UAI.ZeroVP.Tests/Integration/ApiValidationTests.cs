using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UAI.ZeroVP.Tests.Integration;

/// <summary>
/// Verifies standardized error handling (ProblemDetails / ValidationProblemDetails) 
/// and state isolation for invalid API requests (Issue #32 / OVP-T3-13).
/// </summary>
public sealed class ApiValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiValidationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnknownPathUnderApi_Returns404NotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/non-existent-route");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedMethodOnExistingRoute_Returns405MethodNotAllowed_WithAllowHeader()
    {
        // Act
        var response = await _client.DeleteAsync("/api/todos");

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.True(response.Content.Headers.Contains("Allow"));
    }

    [Fact]
    public async Task MalformedJson_Returns400BadRequest()
    {
        // Arrange
        var content = new StringContent("{ malformed json ", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/todos", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyBodyWhereRequired_Returns400BadRequest()
    {
        // Arrange
        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/todos", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingRequiredValue_Returns400BadRequest()
    {
        // Arrange (Title is required)
        var content = new StringContent(
            "{\"title\":\"\",\"priority\":\"Normal\"}",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/todos", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidTypeForKnownField_Returns400BadRequest()
    {
        // Arrange (Passing invalid string for version on PUT endpoint where version is required)
        var content = new StringContent(
            "{\"title\":\"Test\",\"priority\":\"Normal\",\"isCompleted\":false,\"version\":\"not-a-number\"}",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PutAsync($"/api/todos/{Guid.NewGuid()}", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedContentType_Returns415UnsupportedMediaType()
    {
        // Arrange
        var content = new StringContent("title=Test", System.Text.Encoding.UTF8, "text/plain");

        // Act
        var response = await _client.PostAsync("/api/todos", content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task ValidlyFormattedIdOfNonexistentTodo_Returns404NotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/todos/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RejectedWrite_PreservesExistingDataAndVersion()
    {
        // Arrange: Create a valid todo first
        var createContent = new StringContent(
            "{\"title\":\"Initial Valid Todo\",\"priority\":\"Normal\"}",
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await _client.PostAsync("/api/todos", createContent);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var location = createResponse.Headers.Location;
        Assert.NotNull(location);

        // Read created item JSON node directly to avoid DTO enum deserialization issues
        var getCreatedResponse = await _client.GetAsync(location);
        var jsonString = await getCreatedResponse.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(jsonString);

        Assert.NotNull(node);
        var todoId = node["id"]?.ToString();
        var initialVersion = node["version"]?.GetValue<long>();

        Assert.NotNull(todoId);
        Assert.NotNull(initialVersion);

        // Act: Attempt invalid update (stale version 999)
        var staleUpdateContent = new StringContent(
            "{\"title\":\"Attempted Stale Change\",\"priority\":\"High\",\"isCompleted\":false,\"version\":999}",
            System.Text.Encoding.UTF8,
            "application/json");

        var updateResponse = await _client.PutAsync($"/api/todos/{todoId}", staleUpdateContent);

        // Assert: Rejection with 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);

        // Verify: Unchanged original item in storage
        var getResponse = await _client.GetAsync($"/api/todos/{todoId}");
        var currentJson = await getResponse.Content.ReadAsStringAsync();
        var currentNode = JsonNode.Parse(currentJson);

        Assert.NotNull(currentNode);
        Assert.Equal("Initial Valid Todo", currentNode["title"]?.ToString());
        Assert.Equal(initialVersion, currentNode["version"]?.GetValue<long>());
    }
}