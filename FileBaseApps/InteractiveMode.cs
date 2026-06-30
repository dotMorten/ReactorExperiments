#:property TargetFramework=net10.0-windows10.0.22621.0
#:property WindowsAppSDKSelfContained=true
#:package Microsoft.UI.Reactor@0.1.0-*
 
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;
 
if (args.Length > 0 && (args[0] == "-i" || args[0] == "--interactive"))
{
    Console.WriteLine("Waiting for user to enter their name...");
    ReactorApp.Run<EnterUsernameApp>("Username", width: 300, height: 130);
}
else
{
    Console.WriteLine("Enter your user name:");
    AppState.Username = Console.ReadLine() ?? string.Empty;
}
 
if (string.IsNullOrEmpty(AppState.Username))
{
    Console.WriteLine("No username entered.");
    return -1;
}
 
Console.WriteLine($"Hello {AppState.Username}!");
return 0;
 
class EnterUsernameApp : Component
{
    public override Element Render()
    {
        var (name, setName) = UseState("");
 
        return VStack(
            TextBox(name, setName, placeholderText: "Enter username")
                .AutomationName("UsernameInput"),
            Button("Accept", () =>
                {
                    AppState.Username = name;
                    ReactorApp.PrimaryWindow?.Close();
                })
                .IsEnabled(!string.IsNullOrEmpty(name))
                .HAlign(HorizontalAlignment.Stretch)
        ).Padding(10);
    }
}
 
static class AppState
{
    public static string Username { get; set; } = string.Empty;
}