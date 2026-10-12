# SlickFlow

A [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) plugin that brings **SlickRun-style MagicWords** to Flow: short aliases that launch apps, folders, URLs and scripts, chain several launches together, and prompt you for values when needed.

```bash
add g "https://google.com/search?q=<<query>>"   # create an alias from the Flow search bar
g                                               # type it, press Enter, type your search
```

---

## Contents

- [Getting started](#getting-started)
- [Commands](#commands)
- [Advanced features](#advanced-features)
  - [Scripts (PowerShell and Python)](#scripts-powershell-and-python)
  - [Meta items: one alias, many launches](#meta-items-one-alias-many-launches)
  - [Placeholders and prompt mode](#placeholders-and-prompt-mode)
  - [Built-in tokens](#built-in-tokens)
  - [Launch options: admin, window, working folder](#launch-options-admin-window-working-folder)
  - [Icons](#icons)
  - [Context menu](#context-menu)
- [Settings window](#settings-window)
- [Import from SlickRun](#import-from-slickrun)
- [Examples](#examples)

---

## Getting started

1. Install the plugin into Flow Launcher.
2. Open Flow and create an item: `add note notepad.exe`
3. Type `note` and press **Enter**.

SlickFlow has no action keyword, so aliases work straight from the Flow search bar. Exact alias matches rank highest, then aliases that start with what you typed, then close matches.

Any command that targets an existing item accepts **an alias or the item's numeric id**.

---

## Commands

| Command | What it does |
| --- | --- |
| `add <aliases> <target> [args...] [1]` | Create an item |
| `alias <item> <aliases>` | Give an existing item more aliases |
| `remove <alias>` | Remove one alias from its item |
| `delete <item>` | Delete an item and all its aliases |
| `update <item> <property> <value> ...` | Change an item's properties |
| `seticon <item> <path-or-url>` | Set a custom icon |

### `add`

```bash
add <alias1|alias2|...> <target> [args...] [1]
```

- **Aliases**: one or more names separated by `|`. They are stored in lower case.
- **Target**: an executable, a file, a folder, a URL (`https://…`), a script (`.ps1`, `.py`, `.pyw`) or a [meta item](#meta-items-one-alias-many-launches) chain.
- **Args**: anything after the target is passed to it as command-line arguments.
- **`1` at the end** runs the item as Administrator.

```bash
add code|vsc "C:\Program Files\Microsoft VS Code\Code.exe"
add admincmd cmd.exe 1
add yt https://youtube.com
```

> Quotes on the command line only group words together and are removed. If an argument itself needs quotes, set it in the [settings window](#settings-window).

### `alias` and `remove`

```bash
alias yt tube|video     # yt, tube and video now all open YouTube
remove video            # drop just that alias
```

`remove` won't remove an item's last alias; use `delete` for that.

### `update`

```bash
update <item> <property> <value> [<property> <value> ...]
```

| Property | Values |
| --- | --- |
| `args` / `arguments` | Command-line arguments |
| `runas` | `1` = run as Administrator, `0` = normal |
| `startmode` | `0` = normal, `1` = minimized, `2` = maximized |
| `subtitle` | Text shown under the item in Flow |
| `workingdir` / `workdir` | Folder the program starts in |

```bash
update yt subtitle "Open YouTube" startmode 2
```

To change an item's target, edit it in the [settings window](#settings-window).

### `seticon`

```bash
seticon yt https://www.youtube.com/favicon.ico
seticon notes C:\icons\notes.png
```

Accepts a local image or a URL. Downloaded icons are cached in the icon folder.

---

## Advanced features

### Scripts (PowerShell and Python)

Script targets run through their interpreter instead of opening in an editor:

| Extension | Runs with |
| --- | --- |
| `.ps1` | Windows PowerShell, or PowerShell 7 (`pwsh`) if enabled in settings. Starts with `-NoProfile -ExecutionPolicy Bypass`. |
| `.py` | `python.exe` |
| `.pyw` | `pythonw.exe` (no console window) |

```bash
add backup C:\scripts\backup.ps1 -Target D:\
add rename C:\scripts\rename.py --dry-run
```

Python is found on `PATH` unless you set a Python interpreter (an exe or an install folder) in [settings](#settings-window). If PowerShell 7 is enabled but not installed, Windows PowerShell is used.

### Meta items: one alias, many launches

A meta item's target is a list of things to launch, each wrapped in `@`. Each part can be one of your aliases, or a URL, file, folder or program:

```bash
add work @code@@term@@mail@
add morning @https://news.google.com@@mail@@notepad.exe@
```

Typing `work` launches `code`, `term` and `mail` in that order. `morning` opens Google News, your `mail` item and Notepad.

- A part that matches an alias always runs that item.
- Otherwise, a part that looks like a target runs directly: it has a `:` (as in `https:` or `C:`), a `\` or `/`, or a file extension (`notepad.exe`). Direct targets run without arguments.
- A plain word that isn't an alias, like a typo, is reported as an error rather than launched.
- Meta items can include other meta items.
- A loop (an item that ends up including itself) is stopped with an error instead of running forever.
- An alias that doesn't exist is reported as an error rather than silently skipped.

### Placeholders and prompt mode

Put `<<name>>` anywhere in a target or its arguments, and SlickFlow asks for a value when you launch:

| Syntax | Meaning |
| --- | --- |
| `<<name>>` | Ask for `name` |
| `<<name=default>>` | Ask, pre-filled with `default` |
| `<<name\|hint>>` | Ask, showing `hint` as a reminder |
| `<<name=default\|hint>>` | Both |

A name can't contain `<`, `>`, `=` or `|`.

**How prompting works.** When you launch `g` from the example below, the Flow search bar changes to:

```
g | query: 
```

Type the value and press **Enter**. If there are more placeholders, the bar moves on to the next one and keeps what you've filled so far (`g | query=cats | page: `). Pressing **Enter** on the last one launches the item. To cancel, clear the search bar.

```bash
add g "https://google.com/search?q=<<query|search term>>"
add serve python "-m http.server <<port=8080>>"
add ssh wt "ssh <<user=admin>>@<<host|server name>>"
```

A placeholder name that appears more than once is asked for once and filled in everywhere. The same goes for meta items: placeholders from all the items in the chain are collected and asked for once each, so `add stack @api@@web@` asks for a shared `<<port>>` only once.

### Built-in tokens

Placeholders that start with `$` are filled in automatically at launch and never asked for:

| Token | Becomes |
| --- | --- |
| `<<$clip>>` | Clipboard text (empty if there's none) |
| `<<$date>>` | Today's date as `yyyy-MM-dd` |
| `<<$date:FORMAT>>` | Today's date in any [.NET format](https://learn.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings), e.g. `<<$date:dd.MM.yy>>` |
| `<<$time>>` | Current time as `HH:mm` |
| `<<$time:FORMAT>>` | Current time in any format, e.g. `<<$time:HH-mm-ss>>` |
| `<<$env:NAME>>` | Environment variable `NAME` (empty if unset) |

```bash
add tr "https://translate.google.com/?text=<<$clip>>"      # translate the clipboard, no prompt
add log notepad "C:\logs\<<$date>>.txt"                    # open today's log
add home explorer "<<$env:USERPROFILE>>"
```

**Tokens as defaults.** `<<query=$clip>>` asks for `query`, pre-filled with the clipboard text, so you can launch as-is or edit it first:

```bash
add g "https://google.com/search?q=<<query=$clip|search term>>"
```

**Mixing.** An item that only uses tokens launches immediately. An item that mixes tokens and normal placeholders only asks for the normal ones:

```bash
add note notepad "C:\notes\<<$date:yyyyMMdd>>-<<title>>.md"   # asks only for title
```

Details:
- Token names are case-insensitive (`<<$CLIP>>` works). Formats are case-sensitive: `MM` is month, `mm` is minutes.
- Dates and times look the same regardless of your Windows region settings.
- A format can't contain `<`, `>`, `=` or `|`.
- Everything in one launch uses the same moment and the same clipboard contents, including every item in a meta chain.
- A token SlickFlow doesn't recognise, such as `<<$typo>>` or `<<$date:%>>`, is left in the command unchanged, so you can see the mistake.
- Line breaks in clipboard text become spaces when pre-filled into the search bar.

### Launch options: admin, window, working folder

Each item can:
- **Run as Administrator**: `add … 1`, `update <item> runas 1`, or the Run As option in settings. You can also run an executable elevated just once from its [context menu](#context-menu).
- **Start minimized or maximized**: `update <item> startmode 1` (minimized) or `2` (maximized).
- **Start in a specific folder**: `update <item> workdir "D:\projects"`.

### Icons

SlickFlow picks an icon automatically: the program's own icon for files and executables, and the site's favicon for URLs. Icons are saved to the icon folder (configurable in settings). Use `seticon` to override.

### Context menu

Select an item in Flow and press **Shift+Enter** to open its context menu:

| Action | Shown for |
| --- | --- |
| Open File Location | Items that point to an existing file or folder |
| Run as Administrator | Executables that aren't already set to run as Administrator |
| Open in Incognito | URLs, opens a private window in your default browser (Chrome, Edge or Firefox) |
| Open in Terminal / Open in PowerShell | Items that point to an existing file or folder; opens in that folder |
| Executed N times | All items (usage count) |
| Aliases | All items, lists every alias |

---

## Settings window

**Flow Launcher → Settings → Plugins → SlickFlow**

Left side:
- **DB File Path**: where your items are stored. Point it at a synced folder (OneDrive, Dropbox) to share items between PCs.
- **Icon Folder Path**: where icons are saved.
- **Python Interpreter**: `python.exe` or an install folder. Leave empty to use the one on `PATH`.
- **Run .ps1 scripts with PowerShell 7**: use `pwsh` instead of Windows PowerShell.
- **Save / Add Item / Refresh**, and **Import from SlickRun**.
- **?** (bottom left): opens this guide.

Right side: every item, with search and sorting (including by most executed). Click **Edit** on an item to change any field, including its target and aliases.

---

## Import from SlickRun

Click **Import from SlickRun (.qrs)…** in the settings window. The file picker opens in `%APPDATA%\SlickRun`. Choose your live MagicWord list (`SlickRun.srl`) or an exported pack (`*.qrs`).

**The import only adds items.** It never changes or deletes anything you already have:
- A MagicWord whose name you already use as an alias is skipped.
- A name that appears twice in the file is imported once (the first one wins).
- Entries without a filename are skipped.

Afterwards you get a summary, e.g. `Imported 42, skipped 3 (alias already exists: g, mail, calc).`

What carries over:

| SlickRun | SlickFlow |
| --- | --- |
| MagicWord name (`[A,B]` gives two aliases) | Aliases |
| Filename | Target |
| Parameters | Arguments |
| Startup Path | Working folder |
| Notes | Subtitle |
| Run As (elevated) | Run as Administrator. Running as a different user isn't supported and imports as normal. |
| Start Mode | Normal, minimized or maximized |
| Icon | Copied into the icon folder, if the file exists |

SlickRun variables become placeholders:

| SlickRun | SlickFlow |
| --- | --- |
| `$W$`, `$I$` | `<<query>>` (asked for once) |
| `$C$` | `<<$clip>>` |

Other `$X$` variables are left as they are and listed in the summary. SlickRun URL-encodes `$W$`; SlickFlow inserts the text as typed.

SlickRun's built-in commands (a Filename like `@SHUTDOWN@`):

| SlickRun | SlickFlow |
| --- | --- |
| `@MULTI@` | A [meta item](#meta-items-one-alias-many-launches) with the same parts: MagicWord names become alias references, URLs and programs stay as they are. `Params="https://news.google.com@MAIL@notepad.exe"` becomes `@https://news.google.com@@mail@@notepad.exe@`. |
| `@SHUTDOWN@`, `@REBOOT@`, `@LOGOFF@`, `@HIBERNATE@` | `shutdown.exe` with `/s`, `/r`, `/l` or `/h` |
| `@STANDBY@` | Sleep (or hibernate, if hibernation is enabled), as in SlickRun |
| `@LOCK@` | Lock the PC |
| `@SCREENSAVE@` | Start the blank screen saver (`scrnsave.scr`) |
| Anything else, e.g. `@CALENDAR@` | Skipped and listed in the summary as having no SlickFlow equivalent |

If an `@MULTI@` names a MagicWord that is neither in the file nor already in SlickFlow, it is still imported and the summary warns you.

---

## Examples

```bash
# Apps and folders
add note|notepad notepad.exe
add dl explorer "<<$env:USERPROFILE>>\Downloads"
add admincmd cmd.exe 1

# Websites, including search
add yt|youtube https://youtube.com
add gh "https://github.com/search?q=<<query|repo name>>"
add wiki "https://en.wikipedia.org/wiki/<<topic=$clip>>"

# Scripts
add backup C:\scripts\backup.ps1
add todo C:\scripts\todo.py "<<task>>"

# A whole workspace in one word
add work @code@@term@@mail@

# Tweak an item
update yt subtitle "Open YouTube" startmode 2
alias yt tube
seticon yt https://www.youtube.com/favicon.ico

# Clean up
remove tube
delete yt
```

---

Author: Kafu · License: MIT · Version 1.1.0
