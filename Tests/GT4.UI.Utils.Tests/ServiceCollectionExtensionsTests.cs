using FluentAssertions;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class ServiceCollectionExtensionsTests
{
  [Fact]
  public void EveryPhotoEnhancement_IsAlsoRegisteredAsAModelDefinition()
  {
    var services = new ServiceCollection().AddUIUtils();

    var enhancements = ImplementationsOf<IPhotoEnhancement>(services);
    var definitions = ImplementationsOf<IModelDefinition>(services);

    enhancements.Should().NotBeEmpty();
    enhancements.Should().BeSubsetOf(definitions);
  }

  private static Type?[] ImplementationsOf<T>(IServiceCollection services) =>
    services
      .Where(descriptor => descriptor.ServiceType == typeof(T))
      .Select(descriptor => descriptor.ImplementationType)
      .ToArray();
}
