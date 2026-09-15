using GT4.Core.Utils;

namespace GT4.Core.Project.Abstraction;

public interface IMainPersonStore
{
  MainPersonInfo? Get(FileDescription origin);
  void Set(FileDescription origin, int personId, string displayName);
  void Clear(FileDescription origin);
}

public record class MainPersonInfo(int PersonId, string DisplayName);
