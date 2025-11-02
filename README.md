# 🗒️ Sticky Notes (Windows, .NET 8 WinForms)

## 📖 Description

**Sticky Notes** is a lightweight, always-on-top desktop note-taking application for Windows.
Designed with simplicity and performance in mind, it allows you to quickly jot down thoughts, tasks, and reminders without cluttering your workspace.

Unlike bloated note managers, Sticky Notes focuses on the essentials — instant access via hotkey, autosave, and minimal UI.
It quietly lives in your system tray, ready to open or hide with a single shortcut.

Built using **.NET 8 WinForms**, the app is compact, fast, and requires no internet connection or setup.

---

## 🚀 Features

### 🧱 Core

* **Multiple sticky notes:** Create and manage multiple independent sticky notes.
* **Always on top:** The note window stays above all other windows.
* **Minimal UI:** Simple yellow sticky-note style interface with smooth text editing.
* **Tray integration:** Runs silently in the system tray with context menu options.
* **Auto-save:** Notes are automatically saved as you type.
* **Persistent state:** Restores position, size, and text between sessions.
* **Global hotkey:**

  * Default: `Ctrl + Alt + N` — toggle visibility of the note window.
* **Hide instead of close:** Closing the window hides it in the tray, keeping your notes safe.
* **Auto-launch on startup:** Optional startup with Windows.
* **Compact footprint:** Single-file WinForms executable, minimal dependencies.

### ✏️ Text Formatting

* **Rich text formatting:** Bold, italic, underline, and strikethrough styles.
* **Lists support:**
  * Bullet lists with automatic continuation on Enter.
  * Numbered lists with automatic numbering continuation.
* **Clear formatting:** Remove all formatting from selected text.
* **Copy to clipboard:** Quick copy button with visual feedback.
* **Formatting toolbar:** Hover over a note to reveal formatting buttons at the bottom.

---

## 🧩 Context Menu Options

Right-click inside the note or on the tray icon to access:

### Note Context Menu
| Menu Item                      | Action                           |
| ------------------------------ | -------------------------------- |
| **Copy / Paste / Cut / Clear** | Standard text operations         |
| **Minimize to tray**           | Hide the note window             |
| **Exit**                       | Save and quit the app            |

### Tray Icon Context Menu
| Menu Item                      | Action                                      |
| ------------------------------ | ------------------------------------------- |
| **Show / Hide**                | Toggle note visibility from tray            |
| **Always on top**              | Toggle "TopMost" mode for all notes         |
| **Hide taskbar icon**          | Hide/show taskbar icons for note windows    |
| **Run at Windows startup**     | Auto-launch application on Windows startup  |
| **Confirm delete**             | Enable/disable deletion confirmation dialog |
| **Notes**                      | List of all notes with visibility toggle    |
| **New note**                   | Create a new sticky note                     |
| **About**                      | Open GitHub page                             |
| **Exit**                       | Save and quit the app                        |

---

## 💾 Data Storage

All notes and window states are saved to:

```
%AppData%/StickyNotes/data.json
```

This JSON file contains note states and application settings:

```json
{
  "Notes": [
    {
      "Id": "unique-id",
      "Text": "Your saved note text",
      "X": 300,
      "Y": 200,
      "Width": 360,
      "Height": 320
    }
  ],
  "Settings": {
    "HideTaskbarIcon": false,
    "TopMost": true,
    "RunAtStartup": false,
    "ConfirmDelete": true
  }
}
```

---

## ⚙️ Hotkey

By default, a **global hotkey** is registered:

```
Ctrl + Alt + N
```

This allows you to toggle the sticky note visibility even when minimized to the tray.

---

## 🪟 System Tray

When minimized, the app lives in the **Windows system tray** with a small sticky note icon.
Double-click the icon to show or hide all notes.

### Tray Menu Features

* **Notes list:** See all notes with preview text and toggle their visibility.
* **Settings:** Configure always-on-top, taskbar icons, startup, and delete confirmation.
* **Quick actions:** Create new notes, show/hide all, and exit the application.

---

## 🧰 Installation & Running

### Prerequisites

* **Windows 10/11**
* **.NET 8 SDK or runtime**

### Steps

1. Clone or download this repository:

   ```bash
   git clone git@github.com:Levgenij/Sticky-Notes.git
   cd StickyNotes
   ```
2. Build and run:

   ```bash
   dotnet run
   ```

To publish a portable `.exe`:

```bash
dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true -o ./dist
```

For an optimized release build:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:InvariantGlobalization=true -p:DebugType=none
```

---

## 🧠 Shortcuts & UI Controls

### Keyboard Shortcuts

| Action                  | Shortcut           |
| ----------------------- | ------------------ |
| Toggle note visibility  | **Ctrl + Alt + N** |
| Exit application        | From tray → *Exit* |
| Right-click inside note | Context menu       |

### Formatting Toolbar

Hover over a note to reveal the formatting toolbar at the bottom:

| Button | Action                              |
| ------ | ----------------------------------- |
| **B**  | Bold text                           |
| **I**  | Italic text                         |
| **U**  | Underline text                      |
| **S**  | Strikethrough text                  |
| **A̶**  | Clear formatting                     |
| **•**  | Bullet list                         |
| **1.** | Numbered list                       |
| **📋** | Copy to clipboard                   |

### Top Toolbar

Hover over a note to reveal the top toolbar:

| Button | Action               |
| ------ | -------------------- |
| **+**  | Create new note      |
| **–**  | Minimize to tray     |
| **×**  | Delete note (with optional confirmation) |

### Resize Handle

Hover over a note to see the resize handle icon in the bottom-right corner. Drag to resize the note window.

---

## 📝 Formatting Features

### Rich Text Formatting

* Apply **bold**, *italic*, <u>underline</u>, and ~~strikethrough~~ to selected text.
* Remove all formatting with the clear formatting button.

### Lists

* **Bullet lists:** Press Enter after a bullet point to automatically create a new bullet item.
* **Numbered lists:** Press Enter after a numbered item to automatically continue numbering.
* Both list types preserve indentation for nested items.

### Clipboard

* Click the clipboard button to copy selected text (or entire note if nothing is selected).
* The icon changes to a checkmark for 2 seconds to confirm the copy operation.

## 🎯 Future Enhancements

Planned features for future versions:

* Color themes / transparency
* Markdown / checklists support
* Cloud sync (optional)
* Search across all notes

---

## 📜 License

MIT License — free to use, modify, and distribute.

---

