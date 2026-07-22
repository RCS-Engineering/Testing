using System.Windows;
using BalloonPdf.App.Services;

namespace BalloonPdf.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ArialFontResolver.Register();

        base.OnStartup(e);
    }
}
