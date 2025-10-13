using GT4.Project;
using GT4.Utils;

namespace GT4.UI;

public class ServiceBuilder
{
  public static ServiceProvider DefaultServices => new ServiceCollection()
        .BuildDefaultUtils()
        .BuildDefaultProject()
        .BuildServiceProvider();
}
