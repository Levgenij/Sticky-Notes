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

* **Always on top:** The note window stays above all other windows.
* **Minimal UI:** Simple yellow sticky-note style interface with smooth text editing.
* **Tray integration:** Runs silently in the system tray with context menu options.
* **Auto-save:** Notes are automatically saved as you type.
* **Persistent state:** Restores position, size, and text between sessions.
* **Global hotkey:**

  * Default: `Ctrl + Alt + N` — toggle visibility of the note window.
* **Hide instead of close:** Closing the window hides it in the tray, keeping your notes safe.
* **Compact footprint:** Single-file WinForms executable, minimal dependencies.

---

## 🧩 Context Menu Options

Right-click inside the note or on the tray icon to access:

| Menu Item                      | Action                           |
| ------------------------------ | -------------------------------- |
| **Copy / Paste / Cut / Clear** | Standard text operations         |
| **Minimize to tray**           | Hide the note window             |
| **Always on top**              | Toggle “TopMost” mode            |
| **Show / Hide**                | Toggle note visibility from tray |
| **Exit**                       | Save and quit the app            |

---

## 💾 Data Storage

All notes and window states are saved to:

```
%AppData%/StickyNotes/data.json
```

This JSON file contains:

```json
{
  "Text": "Your saved note text",
  "X": 300,
  "Y": 200,
  "Width": 360,
  "Height": 320
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
Double-click the icon to show or hide the note.

---

## 🧰 Installation & Running

### Prerequisites

* **Windows 10/11**
* **.NET 8 SDK or runtime**

### Steps

1. Clone or download this repository:

   ```bash
   git clone git@github.com:Levgenij/Sticky-Notes.git
   cd StickyNote
   ```
2. Build and run:

   ```bash
   dotnet run
   ```

To publish a portable `.exe`:

```bash
dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true -o ./dist
```

---

## 🧠 Shortcuts Summary

| Action                  | Shortcut           |
| ----------------------- | ------------------ |
| Toggle note visibility  | **Ctrl + Alt + N** |
| Exit application        | From tray → *Exit* |
| Right-click inside note | Context menu       |

---

## 🎯 Future Enhancements

Planned features for future versions:

* Multiple sticky notes
* Color themes / transparency
* Markdown / checklists support
* Auto-launch on Windows startup
* Cloud sync (optional)

---

## 📜 License

MIT License — free to use, modify, and distribute.

---

