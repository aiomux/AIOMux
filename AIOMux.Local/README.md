# AIOMux.Local

A lightweight local orchestration tool for AIOMux agents. Run agents interactively, initialize workspaces, and manage configurations with a simple CLI.

## Overview

AIOMux.Local provides:

- **Workspace initialization** - Set up a new AIOMux workspace with one command
- **Interactive REPL** - Chat-like interface for agent execution
- **Configuration management** - JSON-based configuration with validation
- **Plugin discovery** - Automatically load agents from the `skills/` directory
- **Permission system** - Fine-grained control over capabilities (shell execution, file access, etc.)
- **Built-in skills** - NotesSkill for local-first note-taking
- **Event Recording & Replay** - Deterministic, auditable execution with event logs

## Installation

AIOMux.Local is part of the AIOMux solution. Build and run it with:

```bash
dotnet build
dotnet run --project src/AIOMux.Local -- [command] [options]
```

## Usage

### Initialize a Workspace

```bash
dotnet run --project src/AIOMux.Local -- init
```

This creates:
- `aiomux.json` - Main configuration file
- `skills/` - Directory for adding agent plugins
- `sandbox/` - Sandbox directory for safe execution

### Run the REPL

```bash
dotnet run --project src/AIOMux.Local -- run [solutionPath]
```

If `solutionPath` is omitted, the current directory is used.

Once running, you can:
- Type commands and send them to the default agent
- Use built-in commands:
  - `help` - Show available commands
  - `agents` - List loaded agents
  - `config` - Show current configuration
  - `exit` / `quit` - Exit the REPL

### Notes Commands

AIOMux.Local includes a built-in NotesSkill for local-first note-taking:

```bash
# Add a note
dotnet run --project src/AIOMux.Local -- notes add --text "Remember to review PR #123"

# Add a task
dotnet run --project src/AIOMux.Local -- notes add --text "TODO: implement caching #performance"

# Add an idea
dotnet run --project src/AIOMux.Local -- notes add --text "Idea: use Redis for sessions"

# Search notes
dotnet run --project src/AIOMux.Local -- notes search --q "caching"

# List all notes
dotnet run --project src/AIOMux.Local -- notes list
```

Notes are stored locally in `~/.aiomux/notes.json`. See [AIOMux.Skills.Notes](../AIOMux.Skills.Notes/) for details.

### Run Management & Replay

AIOMux.Local includes event recording and deterministic replay:

```bash
# List all recorded runs
dotnet run --project src/AIOMux.Local -- runs list

# Show run details
dotnet run --project src/AIOMux.Local -- runs show <runId>

# Replay a run (no re-execution)
dotnet run --project src/AIOMux.Local -- runs replay <runId>
```

### Demo: NotesSkill with Replay

Test the replay system end-to-end:

```bash
# Execute with recording and immediate replay
dotnet run --project src/AIOMux.Local -- demo notes add --text "Test replay" --replay

# Search notes
dotnet run --project src/AIOMux.Local -- demo notes search --q "replay"

# Replay a specific run
dotnet run --project src/AIOMux.Local -- demo notes replay --run <runId>
```

**Key Features:**
- **Event Recording** - All steps, tool calls, and outputs logged to `~/.aiomux/runs/<runId>.jsonl`
- **Deterministic Replay** - Reconstruct execution without calling LLMs or tools
- **Hash Validation** - Verify integrity with SHA256 hashes
- **Read-Only** - Replay has no side effects

See [DEMO_REPLAY.md](../DEMO_REPLAY.md) and [Replay README](../AIOMux.Core/Replay/README.md) for details.

## Configuration (aiomux.json)

```json
{
  "defaultAgentName": "assistant",
  "model": {
    "provider": "ollama",
    "baseUrl": "http://localhost:11434",
    "modelName": "llama3"
  },
  "skillsPath": "./skills",
  "permissions": {
    "shell.exec": "deny",
    "fs.write": "deny"
  },
  "shell": {
    "sandboxRoot": "./sandbox",
    "timeoutSeconds": 30,
    "maxOutputBytes": 1000000,
    "maxCommandsPerMinute": 60
  }
}
```

### Configuration Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `defaultAgentName` | string | "assistant" | Name of the default agent to use |
| `model.provider` | string | "ollama" | LLM provider: "ollama" or "openai" |
| `model.baseUrl` | string | optional | Base URL for the LLM service |
| `model.apiKeyEnvVar` | string | optional | Environment variable containing API key |
| `model.modelName` | string | "llama3" | Model name to use |
| `skillsPath` | string | "./skills" | Path to skills/plugins directory |
| `permissions.*` | string | varies | Permission modes: "allow", "deny", or "confirm" |
| `shell.*` | object | varies | Shell execution settings |

## Adding Agents

Agents are loaded as plugins from the `skills/` directory. To add an agent:

1. Create an agent plugin that implements `IAgentPlugin` (see [AIOMux.Core](../AIOMux.Core/))
2. Compile it to a `.dll`
3. Place the `.dll` in the `skills/` directory (or a subdirectory)
4. Run `aiomux run` - the plugin will be auto-discovered and loaded

## Architecture

AIOMux.Local is built on top of AIOMux.Core and follows SOLID principles:

- **ConfigLoader** - Loads and validates `aiomux.json`
- **RunnerHost** - Orchestrates the runtime (loads plugins, manages agents)
- **SkillLoader** - Discovers and manages plugin files
- **PermissionService** - Checks capabilities and permissions
- **InitCommand / RunCommand / NotesCommand** - CLI command handlers
- **Program.cs** - Entry point and argument routing

No heavy frameworks are used; the implementation is minimal and focused.

## Permission System

Permissions control access to capabilities. The default configuration denies:
- `shell.exec` - Shell command execution
- `fs.write` - File writing

Change permissions in `aiomux.json` to "allow", "deny", or "confirm" (prompts the user).

## Built-in Skills

### NotesSkill

Local-first note-taking with intelligent processing:
- Automatic classification (note/task/idea/reference)
- Title and summary extraction
- Tag extraction (#hashtags)
- Entity extraction (@mentions, URLs, proper nouns)
- Full-text search
- JSON storage (`~/.aiomux/notes.json`)

See [AIOMux.Skills.Notes README](../AIOMux.Skills.Notes/README.md) for details.

## Requirements

- .NET 8 or later
- AIOMux.Core and AIOMux.Clients (included in solution)

## Development

To extend AIOMux.Local:

1. Add new command types in `Commands/`
2. Handle them in `Program.cs`
3. Use `RunnerHost` to access configuration and orchestration
4. Update the REPL in `RunCommand.cs` if needed

## License

This project is part of AIOMux and is licensed under the MIT License.
