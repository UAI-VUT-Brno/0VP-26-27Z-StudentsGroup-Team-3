using Microsoft.Extensions.DependencyInjection;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.InMemory;

namespace UAI.ZeroVP.Data.DependencyInjection;

/// <summary>
/// Registers the data layer into the dependency injection container.
/// This decouples the API from the specific storage implementation, exposing only <see cref="ITodoRepository"/>.
/// </summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory todo repository as a singleton.
    /// <para>
    /// A singleton lifetime is necessary here: the data is held directly by the repository instance,
    /// so a scoped lifetime would result in an empty store on every request.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback for repository options.</param>
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
}
