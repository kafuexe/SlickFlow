# Flow.Launcher.Plugin.SlickFlow

A lightweight productivity plugin for [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) that lets you create, manage, and launch **custom shortcuts and aliases** for applications, scripts, or files — all from the Flow search bar.

---

## 🚀 Features

- 🧠 **Instant search** for custom aliases and app names
- ⚙️ **Run executables** with arguments and optional admin privileges
- 🏷️ **Add, remove, or update aliases** directly from Flow

---

## Commands

#### ➕ Add a new alias

```bash
add <alias1|alias2|...> <file> [args] [runas]
```

Multiple aliases separated by |
Optional arguments
Optional runas = 1 to run as Administrator

#### 🗑️ Remove an alias (just one)

```bash
remove <alias>
```

Deletes the specified alias from its associated item.
If an item has multiple aliases, only that one is removed.

#### ❌ Delete an entire item

```bash
delete <alias>
```

Removes the item and all aliases associated with it.

#### ✏️ Update an alias

```bash
update <alias> <file> [args] [runas]
```

Replaces the target of an existing alias with a new executable or parameters.

## examples

```bash
 add note|notepad notepad.exe
 add admincmd cmd.exe "" 1

 add yt|youtube "https://youtube.com"
 update yt "https://youtube.com/feed/subscriptions"

 remove note
 delete notepad
```

---

Author: Kafu
License: MIT
Version: 1.0.0
