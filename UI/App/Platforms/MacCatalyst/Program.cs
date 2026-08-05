using ObjCRuntime;
using UIKit;

namespace GT4
{
  public class Program
  {
    // This is the main entry point of the application.
    // if you want to use a different Application Delegate class from "AppDelegate"
    // you can specify it here.
    static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
  }
}
