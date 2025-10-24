using GT4.Core.Project;
using GT4.Core.Utils;

namespace GT4.UI;

public class ServiceBuilder
{
  private static readonly ServiceProvider _DefaultServices = new ServiceCollection()
        .AddSingleton<IDateFormatter, DateFormatter>()
        .AddSingleton<INameFormatter, NameFormatter>()
        .BuildDefaultUtils()
        .BuildDefaultProject()
        .BuildServiceProvider();

  public static ServiceProvider DefaultServices => _DefaultServices;
}
