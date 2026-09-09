# NetSim

A desktop workspace for building network topologies and watching protocols work —
a modern take on tools like Cisco Packet Tracer.

> **Current status:** only the **login / register screen** is being built, and only
> its **display** (no server, no database yet). Everything else is future work.

## Layout

```
FinalProjectCyber/
├── client/                     ← the desktop app
│   ├── NetSim.Client.sln
│   └── NetSim.App/             Avalonia UI 12 · .NET 9 · MVVM
│
└── server/                     ← ASP.NET Core Web API (scaffold only, /health)
    ├── NetSim.Server.sln
    └── NetSim.Server/
```

## Run the client

```bash
dotnet run --project client/NetSim.App
```

A window opens with the sign-in screen; "Create one" switches to the register screen.
Nothing is saved — the buttons only validate the fields and show a message.

## The client files (login / register only)

| # | File | Responsibility |
|---|------|----------------|
| 1 | `Program.cs` | Entry point — starts the Avalonia engine. |
| 2 | `App.axaml` | App-wide setup: the ViewLocator, the base theme, and our `Theme.axaml`. |
| 3 | `App.axaml.cs` | Creates the window and gives it a `MainWindowViewModel`. |
| 4 | `ViewLocator.cs` | Turns a ViewModel object into its matching View (LoginViewModel → LoginView). |
| 5 | `Styles/Theme.axaml` | Colours + control styles (card, inputs, buttons, headings). |
| 6 | `WidthToBool.cs` | Tiny binding helper — hides the branding panel when the window is narrow. |
| 7 | `Views/MainWindow.axaml` | The window frame: branding panel + a card that shows the current screen. |
| 8 | `Views/LoginView.axaml` | The sign-in screen layout. |
| 9 | `Views/RegisterView.axaml` | The create-account screen layout. |
| 10 | `ViewModels/ViewModelBase.cs` | Shared base — gives view models "value changed → refresh the UI". |
| 11 | `ViewModels/MainWindowViewModel.cs` | Navigation — holds and swaps the current screen. |
| 12 | `ViewModels/LoginViewModel.cs` | Sign-in fields, validation, and the button actions. |
| 13 | `ViewModels/RegisterViewModel.cs` | Create-account fields, validation, and the button actions. |

(`*.axaml.cs` files next to the views just call `InitializeComponent()` — no logic.
`NetSim.App.csproj` / `app.manifest` are project config.)
