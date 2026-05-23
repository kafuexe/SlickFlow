# Flow.Launcher.Plugin.SlickFlow

A lightweight productivity plugin for [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) that lets you create, manage, and launch **custom shortcuts and aliases** for applications, scripts, or files — all from the Flow search bar.

---

## 🚀 Features

- 🧠 **Instant search** for custom aliases and app names
- ⚙️ **Run executables** with arguments and optional admin privileges
- 🏷️ **Add, remove, update aliases** directly from Flow
- 🖼️ **Custom icons** per item (local file or URL)
- 🔗 **Meta items** — one alias that chains and launches several others
- 🧩 **Parameterized items** — `<<name>>` placeholders with a guided prompt UX
- 🪟 **Window mode** control (normal / minimized / maximized) and working directory

---

## Commands

> Every command that targets an existing item accepts **either an alias or the item's numeric `id`**.

#### ➕ Add a new item

```bash
add <alias1|alias2|...> <file-or-url> [args...] [runas]
```

- Multiple aliases separated by `|`
- `<file-or-url>` can be an executable, a path, or an `http(s)://` URL
- Optional `args` are forwarded to the process
- Optional trailing `runas = 1` runs the item as Administrator

#### 🏷️ Add aliases to an existing item

```bash
alias <existing-alias-or-id> <newAlias1|newAlias2|...>
```

Adds one or more new aliases to an item that already exists. Duplicates are ignored. Use this when you want a second/third name for an item without re-creating it.

#### 🗑️ Remove a single alias

```bash
remove <alias>
```

Deletes the specified alias from its associated item. If the item only has one alias left, use `delete` instead — `remove` refuses to leave an item nameless.

#### ❌ Delete an entire item

```bash
delete <alias-or-id>
```

Removes the item and **all** aliases associated with it.

#### ✏️ Update item properties

```bash
update <alias-or-id> <property> <value> [<property> <value> ...]
```

Updates one or more properties on an existing item. You can pass several `property value` pairs in a single command.

Supported properties:

| Property              | Description                                                          |
| --------------------- | -------------------------------------------------------------------- |
| `args` / `arguments`  | Command-line arguments passed to the process                         |
| `runas`               | `1` = run as Administrator, `0` = normal                             |
| `startmode`           | Window state: `0` = Normal, `1` = Minimized, `2` = Maximized         |
| `subtitle`            | Subtitle text shown under the item in Flow results                   |
| `workingdir` / `workdir` | Working directory for the launched process                        |

> To change the target file/URL itself, `delete` and re-`add` the item (the file path is not editable via `update`).

#### 🖼️ Set a custom icon

```bash
seticon <alias-or-id> <icon-path-or-url>
```

Assigns a custom icon to the item. The source can be a local file (e.g. `C:\icons\my.png`) or a URL — the plugin downloads and caches it locally.

---

## 🔗 Meta items (alias chains)

A **meta item** is an item whose "file" is a list of other aliases wrapped in `@` markers. Launching a meta item executes every alias in the chain (depth-first), so a single keyword can fire off a whole workspace.

Syntax:

```bash
add <alias> @<alias1>@@<alias2>@@<alias3>@
```

Example — one alias that opens your editor, terminal, and browser tab:

```bash
add work @code@@term@@docs@
```

Meta items can reference other meta items; the chain is cycle-safe (re-entering an ancestor throws cleanly) and unresolved aliases are reported instead of silently skipped.

---

## 🧩 Parameterized items (placeholders)

Embed placeholders inside `FileName` or `arguments` to be prompted for values when launching.

Syntax:

```
<<name>>
<<name=default>>
<<name=default|hint>>
<<name|hint>>
```

- `name` — the parameter name (required, cannot contain `< > = |`)
- `default` — optional default value
- `hint` — optional hint shown beside the prompt

When you launch a parameterized item, SlickFlow enters **prompt mode**: it rewrites the Flow search bar to a guided template and walks you through each placeholder. Press `Enter` to advance to the next one; press `Enter` on the last placeholder to launch.

Prompt-mode query format (you don't type this by hand — SlickFlow builds it for you):

```
<alias> | filled1=value1 | filled2=value2 | currentName: your input here
```

Examples:

```bash
# A parameterized URL — prompts for "query"
add g "https://google.com/search?q=<<query|search term>>"

# Defaults: "port" defaults to 8080 unless overridden
add serve "python -m http.server <<port=8080>>"

# Combine with meta items — placeholders from every leaf are collected,
# deduplicated by name, and prompted in order.
add devstack @api@@web@
```

---

## 📚 Examples

```bash
# Simple shortcuts
add note|notepad notepad.exe
add admincmd cmd.exe "" 1

# URLs
add yt|youtube "https://youtube.com"

# Update properties on an existing item
update yt args "--new-window"
update yt subtitle "Open YouTube" startmode 2

# Add another alias to an existing item
alias yt tube|video

# Custom icon (from a URL)
seticon yt "https://www.youtube.com/favicon.ico"

# Meta item that fires several aliases at once
add work @code@@term@@docs@

# Parameterized item with default + hint
add gh "https://github.com/search?q=<<query|repo name>>"

# Cleanup
remove note
delete notepad
```

---

Author: Kafu
License: MIT
Version: 1.0.0
