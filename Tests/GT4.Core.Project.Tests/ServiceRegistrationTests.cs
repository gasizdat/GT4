using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.Core.Utils.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>Covers the Core.Project DI registration extension.</summary>
public sealed class ServiceRegistrationTests
{
  [Fact]
  public void AddDefaultProject_RegistersProjectListAndCurrentProjectProvider()
  {
    // MainPersonStore needs IConfiguration and the keyed IInteractiveConfiguration it reads/writes
    // through -- mirrors how GT4Services.Add always wires both together.
    var configurationRoot = new ConfigurationBuilder().AddAppConfiguration().Build();

    using var sp = new ServiceCollection()
      .AddCoreUtils()       // supplies IFileSystem / IStorage that ProjectList depends on.
      .AddSingleton<IConfiguration>(configurationRoot)
      .AddActiveConfigurations(configurationRoot)
      .AddDefaultProject()
      .BuildServiceProvider();

    sp.GetService<IProjectList>().Should().NotBeNull();
    sp.GetService<ICurrentProjectProvider>().Should().NotBeNull();
    sp.GetService<IMainPersonStore>().Should().NotBeNull();
  }
}
