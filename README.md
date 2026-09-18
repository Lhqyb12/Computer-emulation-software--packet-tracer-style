# NetSim

A desktop workspace for building network topologies and watching protocols work —
a modern take on tools like Cisco Packet Tracer.

> **Current status:** login/register work end to end against the real server + database.
> The network simulator workspace (topology canvas, device library, the full protocol
> engine) has been ported in from the `NetworkSimulator` reference project and is wired
> in **stage 1** only: it shows and works fully locally/in-memory after sign-in, with no
> server or database involvement yet. Stage 2 (server-side project storage) and stage 3
> (connecting the client to it) are future work.

## Layout

```
FinalProjectCyber/
├── client/                          ← the desktop app
│   ├── NetSim.Client.sln
│   ├── NetSim.App/                  Avalonia UI 12 · .NET 9 · MVVM · composition root
│   ├── NetSim.Core/                 Protocol/domain engine (Ethernet, ARP, IPv4/IPv6, ICMP,
│   │                                TCP, UDP, DNS, DHCP, switching+VLAN, routing, topology)
│   ├── NetSim.Application/          Use-case layer: canvas interaction, device/network/
│   │                                project services - depends on NetSim.Core
│   ├── NetSim.UI/                   Avalonia views/view models for the workspace (canvas,
│   │                                device library, projects, settings) - depends on
│   │                                NetSim.Application
│   ├── NetSim.Core.Tests/
│   └── NetSim.Application.Tests/
│
└── server/                          ← ASP.NET Core Web API (auth: EF Core + PostgreSQL)
    ├── NetSim.Server.sln
    ├── NetSim.Server/
    └── NetSim.Server.Tests/
```

`NetSim.Core`/`NetSim.Application`/`NetSim.UI` were ported from a separate, further-along
reference project (`NetworkSimulator`) that built the simulation engine standalone (no auth,
local LiteDB persistence). Here, persistence for network projects is deferred to a future
stage built on this repo's own server + PostgreSQL instead - there is no
`NetSim.Infrastructure`/LiteDB layer. `NetSim.App/Persistence/InMemoryProjectRepository.cs`
is a stage-1 stand-in so the workspace's project-management commands have something to talk
to until that stage exists.

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
