#if WINDOWS
using GT4.UI.Abstraction;
using Microsoft.ML.OnnxRuntime;

namespace GT4.UI.Utils.Platforms.Windows;

public interface IModelSessionProvider
{
  Task<InferenceSession> GetSessionAsync(ModelDescriptor model, CancellationToken token);
  void Release(ModelDescriptor[] models);
}
#endif
