using GT4.Core.Project;
using GT4.Core.Utils;

namespace GT4.UI;

public class ServiceBuilder
{
  static readonly ServiceProvider _defaultServices = new ServiceCollection()
        .BuildDefaultUtils()
        .BuildDefaultProject()
        .BuildServiceProvider();

  public static ServiceProvider DefaultServices => _defaultServices;
}
