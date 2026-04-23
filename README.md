# DiscordAssistant

A Discord bot built with .NET 10 and Discord.Net. The repository currently includes a reflection-based slash-command layer, sample commands, PostgreSQL-backed data/schedule models, and polling worker scaffolds.

## Setup

1. Create a bot in the Discord Developer Portal.
2. Copy `.env.example` to `.env`.
3. Put your bot token in `.env` as `DISCORD_TOKEN`.
4. Optionally set `DISCORD_GUILD_ID` for fast guild-scoped command syncing while developing.
5. Optionally set `CONNECTION_STRING` to a PostgreSQL connection string if you want to use the database-backed commands and worker layer.
6. Restore dependencies:

```powershell
dotnet restore
```

7. Run the bot:

```powershell
dotnet run
```

## Current Commands

- `/ping` replies with `Pong!`
- `/roll sides:<integer>` rolls a die with the provided number of sides
- `/get name:<string>` reads a value from `data_storage`
- `/set name:<string> data:<string> isprivate:<integer> scope:<string>` writes a value to `data_storage`
- `/brileith_*` commands manage BriLeith recruit setup and subscriptions

## Repository Shape

- `Program.cs` is the current entrypoint. It loads `.env`, starts the Discord client, and syncs slash commands.
- `ICommand.cs`, `CommandHelper.cs`, and `CommandRegistry.cs` provide the current reflection-based slash-command abstraction.
- `Commands/BasicCommands.cs` is the current command registration point used by the bot.
- `Commands/BrileithCommands.cs` contains the BriLeith-specific slash commands.
- `DBModels/*.cs` defines the current PostgreSQL table shape used by Dapper.
- `Workers/WorkerBase.cs` contains the shared schedule polling worker and cron matching logic.
- `Workers/BrileithRecruitWorker.cs` loads recruit rows, filters matching targets, and posts the formatted message to Discord.
- `Utilities.cs` exposes the SQL bootstrap script for the current database tables.

## Database Notes

The database layer is written for PostgreSQL because the project references `Npgsql` and queries through `Dapper`.

The bootstrap SQL currently creates:

- `schedule`
- `schedule_subscriber`
- `data_storage`
- `brileith_recruit`
- `brileith_recruit_target`

`schedule` columns:

- `id BIGSERIAL PRIMARY KEY`
- `cron_expression VARCHAR(32) NOT NULL`
- `message_template VARCHAR(512) NOT NULL`

`schedule_subscriber` columns:

- `id BIGSERIAL PRIMARY KEY`
- `schedule_id BIGINT NOT NULL REFERENCES schedule(id) ON DELETE CASCADE`
- `subscriber_id BIGINT NOT NULL`
- `subscriber_channel_id BIGINT NOT NULL`

`data_storage` columns:

- `id BIGSERIAL PRIMARY KEY`
- `name VARCHAR(128) NOT NULL`
- `data VARCHAR(1024) NOT NULL`
- `guild_id BIGINT NULL`
- `channel_id BIGINT NULL`
- `user_id BIGINT NULL`
- `scope_expression VARCHAR(64) NULL`

`brileith_recruit` columns:

- `id BIGSERIAL PRIMARY KEY`
- `channel_id BIGINT NOT NULL`
- `recruit_message VARCHAR(512) NOT NULL`
- `recruit_time_regex VARCHAR(32) NOT NULL`

`brileith_recruit_target` columns:

- `id BIGSERIAL PRIMARY KEY`
- `recruit_id BIGINT NOT NULL REFERENCES brileith_recruit(id) ON DELETE CASCADE`
- `target_id BIGINT NOT NULL`
- `recruit_time_regex VARCHAR(32) NOT NULL`

`schedule.cron_expression`, `brileith_recruit.recruit_time_regex`, and `brileith_recruit_target.recruit_time_regex` expect a five-part cron expression:

```text
minute hour day-of-month month day-of-week
```

Examples:

- `0 21 * * *`
- `*/15 * * * *`
- `0 9 * * MON-FRI`

The current worker supports:

- `*`
- comma-separated values
- numeric ranges like `1-5`
- step syntax like `*/5` and `1-10/2`
- weekday aliases like `MON` through `SUN`

`WorkerBase` reads one `schedule` row by id, loads `schedule_subscriber` rows for it, groups subscribers by `subscriber_channel_id`, and replaces `{USER_ID}` in the message template with Discord mentions.

`BrileithRecruitWorker` reads one `brileith_recruit` row by id, loads all matching `brileith_recruit_target` rows, filters them by cron match, and injects matching `target_id` values into the outgoing message formatter.

Startup wiring in `Program.cs` is still minimal and does not yet automatically initialize the database or start workers beyond the current manual setup in code.

## Development Note

Set `DISCORD_GUILD_ID` in `.env` while developing to sync slash commands to a single server immediately. Without it, commands are synced globally and can take longer to appear.
