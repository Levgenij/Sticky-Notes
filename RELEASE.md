# Release Notes

## Version 1.1.0 - Bug Fixes and Improvements

**Release Date:** 04.11.2025

### 🐛 Bug Fixes

* **Fixed double symbol deletion** — Resolved issue with duplicate symbols appearing when deleting text
* **Fixed WndProc override** — Corrected window procedure override implementation

### ✨ Improvements

* **Enhanced list handling** — Improved behavior of bullet and numbered lists during editing
* **Improved ordered list deletion** — Better handling of numbered lists when deleting rows, maintaining proper numbering sequence
* **Build optimization** — Optimized build configuration and project settings for better performance

---

## Version 1.0.0 - Initial Release

**Release Date:** 02.11.2025

### 🎉 First Release

This is the initial release of **Sticky Notes** — a lightweight, always-on-top desktop note-taking application for Windows built with .NET 8 WinForms.

### ✨ Features

#### Core Functionality

* **Multiple sticky notes** — Create and manage multiple independent sticky notes
* **Always on top** — Note windows stay above all other windows
* **Minimal UI** — Simple yellow sticky-note style interface with smooth text editing
* **System tray integration** — Runs silently in the system tray with context menu options
* **Auto-save** — Notes are automatically saved as you type
* **Persistent state** — Restores position, size, and text between sessions
* **Global hotkey** — `Ctrl + Alt + N` to toggle note visibility
* **Hide instead of close** — Closing the window hides it in the tray, keeping notes safe
* **Auto-launch on startup** — Optional startup with Windows
* **Single-file executable** — Compact footprint with minimal dependencies

#### Text Formatting

* **Rich text formatting** — Bold, italic, underline, and strikethrough styles
* **Bullet lists** — Automatic continuation on Enter
* **Numbered lists** — Automatic numbering continuation
* **Clear formatting** — Remove all formatting from selected text
* **Copy to clipboard** — Quick copy button with visual feedback
* **Formatting toolbar** — Hover over a note to reveal formatting buttons at the bottom

#### User Interface

* **Top toolbar** — Create new notes, minimize to tray, delete notes
* **Formatting toolbar** — Text formatting controls (bold, italic, underline, strikethrough, lists)
* **Resize handle** — Drag to resize note windows
* **Context menus** — Right-click in note or on tray icon for quick actions

#### Settings & Configuration

* **Always on top toggle** — Enable/disable TopMost mode for all notes
* **Taskbar icon visibility** — Hide/show taskbar icons for note windows
* **Windows startup** — Auto-launch application on Windows startup
* **Delete confirmation** — Enable/disable deletion confirmation dialog
* **Notes list** — See all notes with preview text and toggle their visibility

### 📦 Technical Details

* **Platform:** Windows 10/11
* **Framework:** .NET 8 WinForms
* **Architecture:** win-x64
* **Build:** Single-file, self-contained executable
* **Data Storage:** JSON file in `%AppData%/StickyNotes/data.json`

### 🔧 Build Instructions

To build the release version:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:InvariantGlobalization=true -p:DebugType=none
```

### 📝 Notes

* All notes and settings are stored locally in the user's AppData folder
* No internet connection required
* No installation required — just run the executable
* Fully portable single-file application

---

**License:** MIT License — free to use, modify, and distribute.

