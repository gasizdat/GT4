using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.Core.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>Covers the Core.Project DI registration extension.</summary>
public sealed class ServiceRegistrationTests
{
  [Fact]
  public void AddDefaultProject_RegistersProjectListAndCurrentProjectProvider()
  {
    using var sp = new ServiceCollection()
      .AddCoreUtils()       // supplies IFileSystem / IStorage that ProjectList depends on.
      .AddDefaultProject()
      .BuildServiceProvider();

    sp.GetService<IProjectList>().Should().NotBeNull();
    sp.GetService<ICurrentProjectProvider>().Should().NotBeNull();
  }
}
