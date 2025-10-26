using System;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using GT4.UI.App;

namespace GT4
{
    internal class Program : MauiApplication
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        static void Main(string[] args)
        {
            var app = new Program();
            app.Run(args);
        }
    }
}
