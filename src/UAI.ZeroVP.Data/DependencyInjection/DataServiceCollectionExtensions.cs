using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.Data.Json;

namespace UAI.ZeroVP.Data.DependencyInjection;

/// <summary>
/// Service collection extensions for configuring todo storage implementations.
/// </summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory todo repository.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options configuration delegate.</param>
    public static IServiceCollection AddInMemoryTodoData(
        this IServiceCollection services,
        Action<InMemoryTodoOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new InMemoryTodoOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ITodoRepository>(sp => new InMemoryTodoRepository(
            sp.GetRequiredService<InMemoryTodoOptions>(),
            sp.GetService<TimeProvider>()));

        return services;
    }

    /// <summary>
    /// Registers the persistent JSON file todo repository as a singleton.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options configuration delegate.</param>
    public static IServiceCollection AddJsonFileTodoData(
        this IServiceCollection services,
        Action<JsonFileTodoOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new JsonFileTodoOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ITodoRepository>(sp => new JsonFileTodoRepository(
            sp.GetRequiredService<JsonFileTodoOptions>(),
            sp.GetService<TimeProvider>(),
            sp.GetService<ILogger<JsonFileTodoRepository>>()));

        return services;
    }
}
