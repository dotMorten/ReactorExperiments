#:property TargetFramework=net10.0-windows10.0.22621.0
#:property WindowsAppSDKSelfContained=true
#:package Microsoft.UI.Reactor@0.1.0-*
 
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;
 
ReactorApp.Run<App>("SingleFileReactor", width: 900, height: 600);
 
class App : Component
{
    public override Element Render()
    {
        var (name, setName) = UseState("World");
 
        return VStack(
            Heading($"Hello, {name}!"),
            TextBox(name, setName, placeholderText: "Your name")
                .AutomationName("NameInput")
        ).Padding(16);
    }
}