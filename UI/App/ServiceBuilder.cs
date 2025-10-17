using GT4.Core.Project;
using GT4.Core.Utils;

namespace GT4.UI;

public class ServiceBuilder
{
  public static ServiceProvider DefaultServices => new ServiceCollection()
        .BuildDefaultUtils()
        .BuildDefaultProject()
        .BuildServiceProvider();
}
