using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace ReactorTrayWorker;

internal sealed class SettingsWindow : Component
{
    public override Element Render()
    {
        var (name, setName) = UseState("World");

        return 
            VStack(
            Heading($"Hello, {name}!"),
            TextBox(name, setName, placeholderText: "Your name")
                .AutomationName("NameInput")
        ).Padding(16);
    }
}
