# Official submission preparation

Based on release 0.11.1. This branch adds the installer icon and procedural-sound disclosure to the plugin metadata, restores repository/icon links, and resolves TerraFX through the SDK's `DalamudLibPath`. Runtime C# code and the five embedded relay scripts are unchanged.

On 9 September 2026, Aleqsd confirmed personally testing 0.11.1 in FFXIV with the latest game update and that it works. The metadata-only submission build was compiled separately with .NET 10.0.400 and Dalamud 15.0.3.3: locked restore and Release build passed with no warnings or errors. It was not loaded into the running game by the preparation task.

The generated manifest declares API 15, version 0.11.1.0, repository/icon links and Codex-created assets. Packager creates the installation archive; no Node.js, Codex CLI, fonts or host libraries are bundled. Existing release assets and the custom catalogue are unchanged.

For review, open `/codex`, then Settings → Connection to start the included relay. Node.js 22.22.2+ and the Codex desktop app are external prerequisites; usage limits also require a signed-in Codex CLI. Automatic relay startup is optional and disabled by default. An existing external relay remains independent.

The relay observes local task metadata and status over internal Codex protocols and serves read-only loopback endpoints. Optional question excerpts are limited to 240 characters and removed from saved history. Answers and tool output are not forwarded to the game. Task links open existing tasks after an explicit click; they do not send prompts or approve work. See [validation and data limits](VALIDATION.md).

AI usage: Auto (OpenAI Codex). Aleqsd chose features, tested in game and guided iterations. Codex wrote most of the implementation, including autonomous passes, and helped prepare the submission. The installer icon and procedural sounds were made with Codex; sounds can be disabled or replaced. Aleqsd is happy to redraw the icon by hand if needed.
