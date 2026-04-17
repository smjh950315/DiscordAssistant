# DiscordAssistant

A basic Discord bot built with .NET and Discord.Net.

## Setup

1. Create a bot in the Discord Developer Portal.
2. Copy `.env.example` to `.env`.
3. Put your bot token in `.env` as `DISCORD_TOKEN`.
4. Restore dependencies:

```powershell
dotnet restore
```

5. Run the bot:

```powershell
dotnet run
```

## Commands

- `/ping` replies with `Pong!`
- `/hello` greets the user who ran the command.

## Development Notes

Set `DISCORD_GUILD_ID` in `.env` while developing to sync slash commands to a single server immediately. Without it, commands are synced globally and can take longer to appear.
