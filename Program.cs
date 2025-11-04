using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;
using Svg;
using System.Reflection;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var app = new StickyApp();
        Application.Run(app);
    }
}

static class ResourceHelper
{
    static readonly Assembly assembly = Assembly.GetExecutingAssembly();
    static readonly string assemblyName = assembly.GetName().Name ?? "";

    public static Stream? GetResourceStream(string resourceName)
    {
        var normalizedName = resourceName.Replace('/', '.').Replace('\\', '.');
        var fullName = $"{assemblyName}.{normalizedName}";
        var stream = assembly.GetManifestResourceStream(fullName);
        if (stream != null) return stream;
        
        fullName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(normalizedName, StringComparison.OrdinalIgnoreCase));
        return fullName != null ? assembly.GetManifestResourceStream(fullName) : null;
    }

    public static Icon? LoadIconFromResource(string resourceName)
    {
        try
        {
            using var stream = GetResourceStream(resourceName);
            if (stream != null)
            {
                return new Icon(stream);
            }
        }
        catch { }
        return null;
    }

    public static SvgDocument? LoadSvgFromResource(string resourceName)
    {
        try
        {
            using var stream = GetResourceStream(resourceName);
            if (stream != null)
            {
                return SvgDocument.Open<SvgDocument>(stream);
            }
        }
        catch { }
        return null;
    }
}

public class StickyApp : ApplicationContext
{

    readonly List<NoteForm> notes;
    readonly NotifyIcon tray;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem showHideItem = null!;
    readonly ToolStripMenuItem topMostItem = null!;
    readonly ToolStripMenuItem hideTaskbarItem = null!;
    readonly ToolStripMenuItem runAtStartupItem = null!;
    readonly ToolStripMenuItem confirmDeleteItem = null!;
    readonly ToolStripMenuItem notesMenuItem = null!;
    readonly string dataPath;
    readonly Icon? customIcon;
    NoteForm? lastActiveNote;

    public StickyApp()
    {
        notes = new List<NoteForm>();
        dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes", "data.json");
        Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);

        menu = new ContextMenuStrip();
        showHideItem = new ToolStripMenuItem("Show");
        showHideItem.Click += (_, __) => ToggleNotes();

        topMostItem = new ToolStripMenuItem("Always on top") { CheckOnClick = true };
        topMostItem.CheckedChanged += (_, __) => 
        { 
            SetAllTopMost(topMostItem.Checked);
            SaveSettings();
        };

        hideTaskbarItem = new ToolStripMenuItem("Hide taskbar icon") { CheckOnClick = true };
        hideTaskbarItem.CheckedChanged += (_, __) => 
        { 
            SetAllShowInTaskbar(!hideTaskbarItem.Checked);
            SaveSettings();
        };

        runAtStartupItem = new ToolStripMenuItem("Run at Windows startup") { CheckOnClick = true };
        runAtStartupItem.CheckedChanged += (_, __) => 
        { 
            SetRunAtStartup(runAtStartupItem.Checked);
            SaveSettings();
        };

        confirmDeleteItem = new ToolStripMenuItem("Confirm delete") { CheckOnClick = true };
        confirmDeleteItem.CheckedChanged += (_, __) => SaveSettings();

        notesMenuItem = new ToolStripMenuItem("Notes");
        notesMenuItem.DropDownItems.Clear();

        var newNoteItem = new ToolStripMenuItem("New note");
        newNoteItem.Click += (_, __) => CreateNewNote();

        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += (_, __) => OpenGitHubPage();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, __) => ExitApp();

        menu.Items.AddRange(new ToolStripItem[] { showHideItem, topMostItem, hideTaskbarItem, runAtStartupItem, confirmDeleteItem, new ToolStripSeparator(), notesMenuItem, newNoteItem, new ToolStripSeparator(), aboutItem, exitItem });

        try
        {
            customIcon = ResourceHelper.LoadIconFromResource("icon.ico");
        }
        catch (Exception ex) { Console.WriteLine(ex); }

        tray = new NotifyIcon
        {
            Icon = customIcon ?? SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu,
            Text = "Sticky Notes"
        };
        tray.DoubleClick += (_, __) => ToggleNotes();

        var collection = NoteStateCollection.Load(dataPath);
        var states = collection.Notes;
        if (states.Count == 0)
        {
            states.Add(new NoteState { Id = Guid.NewGuid().ToString() });
        }

        if (collection.Settings != null)
        {
            if (hideTaskbarItem != null)
            {
                hideTaskbarItem.Checked = collection.Settings.HideTaskbarIcon;
            }
            if (topMostItem != null)
            {
                topMostItem.Checked = collection.Settings.TopMost;
            }
            if (runAtStartupItem != null)
            {
                var isCurrentlyEnabled = IsRunAtStartupEnabled();
                runAtStartupItem.Checked = collection.Settings.RunAtStartup ?? isCurrentlyEnabled;
                if (runAtStartupItem.Checked != isCurrentlyEnabled)
                {
                    SetRunAtStartup(runAtStartupItem.Checked);
                }
            }
            if (confirmDeleteItem != null)
            {
                confirmDeleteItem.Checked = collection.Settings.ConfirmDelete ?? true;
            }
        }
        else
        {
            if (confirmDeleteItem != null)
            {
                confirmDeleteItem.Checked = true;
            }
        }

        foreach (var state in states)
        {
            CreateNote(state);
        }

        if (collection.Settings != null)
        {
            if (hideTaskbarItem != null)
            {
                SetAllShowInTaskbar(!hideTaskbarItem.Checked);
            }
            if (topMostItem != null)
            {
                SetAllTopMost(topMostItem.Checked);
            }
        }

        UpdateTopMostMenu();
        UpdateTrayText();
        UpdateNotesMenu();
    }

    void CreateNote(NoteState state)
    {
        var note = new NoteForm(state, this);
        note.VisibleChanged += (_, __) => 
        {
            UpdateTrayText();
            UpdateNotesMenu();
        };
        note.FormClosed += (_, __) => RemoveNote(note);
        note.RequestExit += (_, __) => ExitApp();
        note.RequestNewNote += (sender, __) => CreateNewNote(sender as NoteForm);
        note.RequestDelete += (_, __) => DeleteNote(note);
        note.NoteTextChanged += (_, __) => UpdateNotesMenu();
        note.Activated += (_, __) => lastActiveNote = note;
        note.GotFocus += (_, __) => lastActiveNote = note;
        note.MouseDown += (_, __) => lastActiveNote = note;
        notes.Add(note);
        if (hideTaskbarItem != null)
        {
            note.ShowInTaskbar = !hideTaskbarItem.Checked;
        }
        if (topMostItem != null)
        {
            note.TopMost = topMostItem.Checked;
        }
        note.Show();
        UpdateTopMostMenu();
        UpdateNotesMenu();
    }

    void CreateNewNote(NoteForm? sourceNote = null)
    {
        NoteForm? referenceNote = sourceNote ?? lastActiveNote ?? notes.Where(n => n.Visible).LastOrDefault();
        var state = new NoteState
        {
            Id = Guid.NewGuid().ToString(),
            X = referenceNote != null ? referenceNote.Left + 30 : 100,
            Y = referenceNote != null ? referenceNote.Top + 30 : 100
        };
        CreateNote(state);
    }

    void RemoveNote(NoteForm note)
    {
        note.SaveState();
        notes.Remove(note);
        SaveAllStates();
        UpdateTrayText();
        UpdateNotesMenu();
    }

    void DeleteNote(NoteForm note)
    {
        bool shouldDelete = true;
        
        if (confirmDeleteItem != null && confirmDeleteItem.Checked)
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete this note?",
                "Delete Note",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            
            shouldDelete = result == DialogResult.Yes;
        }
        
        if (shouldDelete)
        {
            note.SaveState();
            notes.Remove(note);
            note.Close();
            SaveAllStates();
            UpdateTrayText();
            UpdateNotesMenu();
        }
    }

    void ToggleNotes()
    {
        if (notes.Count == 0)
        {
            CreateNewNote();
            return;
        }
        var anyVisible = notes.Any(n => n.Visible);
        foreach (var note in notes)
        {
            if (anyVisible)
            {
                note.Hide();
            }
            else
            {
                note.Show();
                note.Activate();
            }
        }
        UpdateTrayText();
        UpdateNotesMenu();
    }

    void UpdateTopMostMenu()
    {
        if (topMostItem == null || notes.Count == 0) return;
        topMostItem.Checked = notes[0].TopMost;
    }

    void SetAllTopMost(bool topMost)
    {
        foreach (var note in notes)
        {
            note.TopMost = topMost;
        }
    }

    void SetAllShowInTaskbar(bool showInTaskbar)
    {
        foreach (var note in notes)
        {
            note.ShowInTaskbar = showInTaskbar;
        }
    }

    void UpdateTrayText()
    {
        if (showHideItem == null || tray == null) return;
        var visibleCount = notes.Count(n => n.Visible);
        showHideItem.Text = visibleCount > 0 ? "Hide all" : "Show all";
        tray.Text = $"Sticky Notes ({notes.Count})";
    }

    void UpdateNotesMenu()
    {
        if (notesMenuItem == null) return;

        notesMenuItem.DropDownItems.Clear();

        if (notes.Count == 0)
        {
            notesMenuItem.Enabled = false;
            return;
        }

        notesMenuItem.Enabled = true;

        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var text = note.GetText();
            var cleanText = text?.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim() ?? string.Empty;
            var preview = string.IsNullOrWhiteSpace(cleanText) ? "(empty)" : cleanText.Length > 20 ? cleanText.Substring(0, 20) : cleanText;
            preview += " ";
            var status = note.Visible ? "+" : "-";
            var menuItem = new ToolStripMenuItem($"{i + 1}. {status} {preview}");
            menuItem.Click += (_, __) => 
            {
                if (note.Visible)
                {
                    note.Hide();
                }
                else
                {
                    note.Show();
                    note.Activate();
                }
                UpdateTrayText();
            };
            notesMenuItem.DropDownItems.Add(menuItem);
        }
    }

    void ExitApp()
    {
        SaveAllStates();
        tray.Visible = false;
        ExitThread();
    }

    void SaveAllStates()
    {
        var states = notes.Select(n => n.GetState()).ToList();
        NoteStateCollection.Save(dataPath, states, GetSettings());
    }

    AppSettings GetSettings()
    {
        return new AppSettings
        {
            HideTaskbarIcon = hideTaskbarItem?.Checked ?? false,
            TopMost = topMostItem?.Checked ?? false,
            RunAtStartup = runAtStartupItem?.Checked ?? false,
            ConfirmDelete = confirmDeleteItem?.Checked ?? true
        };
    }

    bool IsRunAtStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            if (key == null) return false;
            var value = key.GetValue("StickyNotes");
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    void SetRunAtStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Application.ExecutablePath;
                key.SetValue("StickyNotes", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("StickyNotes", false);
            }
        }
        catch { }
    }

    void OpenGitHubPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Levgenij/Sticky-Notes",
                UseShellExecute = true
            });
        }
        catch { }
    }

    void SaveSettings()
    {
        var states = notes.Select(n => n.GetState()).ToList();
        NoteStateCollection.Save(dataPath, states, GetSettings());
    }

    public void SaveAllStatesRequested()
    {
        SaveAllStates();
    }
}

class BorderPanel : Panel
{
    readonly bool drawAll;

    public BorderPanel(bool drawAll = false)
    {
        this.drawAll = drawAll;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (drawAll)
        {
            using var pen = new Pen(Color.FromArgb(140, 140, 140), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }
}

class ScrollbarlessRichTextBox : RichTextBox
{
    const int SB_VERT = 1;

    [DllImport("user32.dll")]
    static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

    protected override void WndProc(ref Message m)
    {
        try
        {
            base.WndProc(ref m);
        }
        catch (System.Globalization.CultureNotFoundException)
        {
        }
        
        if (IsHandleCreated)
        {
            ShowScrollBar(Handle, SB_VERT, false);
        }
    }
}

public class NoteForm : Form
{
    const int MOD_ALT = 0x0001;
    const int MOD_CONTROL = 0x0002;
    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 0x7777;
    const int WM_NCLBUTTONDOWN = 0xA1;
    const int HT_CAPTION = 0x2;
    const int CS_DROPSHADOW = 0x00020000;

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [DllImport("user32.dll")]
    static extern bool ReleaseCapture();

    readonly ScrollbarlessRichTextBox editor;
    readonly Label addButton;
    readonly Label closeButton;
    readonly Label deleteButton;
    readonly Label boldButton;
    readonly Label italicButton;
    readonly Label underlineButton;
    readonly Label strikethroughButton;
    readonly Label listButton;
    readonly Label listOrderedButton;
    readonly Label removeFormattingButton;
    readonly Label clipboardButton;
    readonly Panel formatToolbar;
    readonly Label resizeIcon;
    System.Windows.Forms.Timer? clipboardIconTimer;
    bool isClipboardCheckIcon;
    readonly System.Windows.Forms.Timer autosaveTimer;
    readonly System.Windows.Forms.Timer hideToolbarTimer;
    readonly string dataPath;
    readonly string noteId;
    readonly StickyApp? app;
    bool isResizing;
    Point resizeStartPos;
    Size resizeStartSize;
    public event EventHandler? RequestExit;
    public event EventHandler? RequestNewNote;
    public event EventHandler? RequestDelete;
    public event EventHandler? NoteTextChanged;

    public NoteForm(NoteState state, StickyApp? app = null)
    {
        this.app = app;
        noteId = state.Id;
        Text = "Sticky Notes";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        BackColor = Color.FromArgb(255, 255, 248, 180);
        MinimumSize = new Size(250, 180);
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);

        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
        }
        catch (Exception ex) { Console.WriteLine(ex); }

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 0,
            BackColor = Color.FromArgb(255, 255, 248, 180)
        };
        toolbar.MouseDown += NoteForm_MouseDown;
        toolbar.Resize += (_, __) => UpdateButtonPositions();

        addButton = new Label
        {
            Text = "+",
            Font = new Font("Segoe UI", 14f, FontStyle.Regular),
            Size = new Size(30, 30),
            Anchor = AnchorStyles.None,
            Location = new Point(5, 5),
            BackColor = Color.FromArgb(255, 255, 248, 180),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Cursor = Cursors.Hand
        };
        addButton.Click += (_, __) => RequestNewNote?.Invoke(this, EventArgs.Empty);
        toolbar.Controls.Add(addButton);

        deleteButton = new Label
        {
            Text = "×",
            Font = new Font("Segoe UI", 14f, FontStyle.Regular),
            Size = new Size(30, 30),
            Anchor = AnchorStyles.None,
            BackColor = Color.FromArgb(255, 255, 248, 180),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Cursor = Cursors.Hand
        };
        deleteButton.Click += (_, __) => RequestDelete?.Invoke(this, EventArgs.Empty);
        toolbar.Controls.Add(deleteButton);

        closeButton = new Label
        {
            Text = "–",
            Font = new Font("Segoe UI", 14f, FontStyle.Regular),
            Size = new Size(30, 30),
            Anchor = AnchorStyles.None,
            BackColor = Color.FromArgb(255, 255, 248, 180),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Cursor = Cursors.Hand
        };
        closeButton.Click += (_, __) => Hide();
        toolbar.Controls.Add(closeButton);

        editor = new ScrollbarlessRichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            DetectUrls = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Font = new Font("Segoe UI", 10f),
            BackColor = Color.FromArgb(255, 255, 248, 180)
        };

        var cms = new ContextMenuStrip();
        var copy = new ToolStripMenuItem("Copy", null, (_, __) => editor.Copy());
        var paste = new ToolStripMenuItem("Paste", null, (_, __) => editor.Paste());
        var cut = new ToolStripMenuItem("Cut", null, (_, __) => editor.Cut());
        var clear = new ToolStripMenuItem("Clear", null, (_, __) => editor.Clear());
        var minimizeToTray = new ToolStripMenuItem("Minimize to tray", null, (_, __) => Hide());
        var exit = new ToolStripMenuItem("Exit", null, (_, __) => RequestExit?.Invoke(this, EventArgs.Empty));
        cms.Items.AddRange(new ToolStripItem[] { copy, paste, cut, clear, new ToolStripSeparator(), minimizeToTray, exit });
        editor.ContextMenuStrip = cms;

        formatToolbar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 0,
            BackColor = Color.FromArgb(255, 255, 248, 180)
        };
        formatToolbar.MouseDown += NoteForm_MouseDown;

        boldButton = CreateFormatButton("assests/icons/bold.svg", ToggleBold);
        formatToolbar.Controls.Add(boldButton);

        italicButton = CreateFormatButton("assests/icons/italic.svg", ToggleItalic);
        formatToolbar.Controls.Add(italicButton);

        underlineButton = CreateFormatButton("assests/icons/underline.svg", ToggleUnderline);
        formatToolbar.Controls.Add(underlineButton);

        strikethroughButton = CreateFormatButton("assests/icons/strikethrough.svg", ToggleStrikethrough);
        formatToolbar.Controls.Add(strikethroughButton);

        removeFormattingButton = CreateFormatButton("assests/icons/remove-formatting.svg", ClearFormatting);
        formatToolbar.Controls.Add(removeFormattingButton);

        listButton = CreateFormatButton("assests/icons/list.svg", InsertList);
        formatToolbar.Controls.Add(listButton);

        listOrderedButton = CreateFormatButton("assests/icons/list-ordered.svg", InsertOrderedList);
        formatToolbar.Controls.Add(listOrderedButton);

        clipboardButton = new Label
        {
            Size = new Size(28, 28),
            Anchor = AnchorStyles.None,
            BackColor = Color.FromArgb(255, 255, 248, 180),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Cursor = Cursors.Hand,
            ImageAlign = ContentAlignment.MiddleCenter
        };
        
        try
        {
            var bitmap = LoadSvgAsBitmap("assests/icons/clipboard.svg", 20, 20, Color.FromArgb(100, 100, 100));
            clipboardButton.Image = bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading clipboard SVG: {ex}");
        }
        
        clipboardButton.Click += (_, __) => CopyToClipboard();
        clipboardButton.MouseEnter += (_, __) => 
        {
            clipboardButton.BackColor = Color.FromArgb(255, 255, 200);
            try
            {
                var resourceName = isClipboardCheckIcon ? "assests/icons/clipboard-check.svg" : "assests/icons/clipboard.svg";
                var blackBitmap = LoadSvgAsBitmap(resourceName, 20, 20, Color.Black);
                clipboardButton.Image?.Dispose();
                clipboardButton.Image = blackBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating clipboard icon on hover: {ex}");
            }
        };
        clipboardButton.MouseLeave += (_, __) => 
        {
            clipboardButton.BackColor = Color.FromArgb(255, 255, 248, 180);
            try
            {
                var resourceName = isClipboardCheckIcon ? "assests/icons/clipboard-check.svg" : "assests/icons/clipboard.svg";
                var grayBitmap = LoadSvgAsBitmap(resourceName, 20, 20, Color.FromArgb(100, 100, 100));
                clipboardButton.Image?.Dispose();
                clipboardButton.Image = grayBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating clipboard icon on leave: {ex}");
            }
        };
        
        formatToolbar.Controls.Add(clipboardButton);

        formatToolbar.Resize += (_, __) => UpdateFormatButtonPositions();

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 50, 10, 50),
            BackColor = Color.FromArgb(255, 255, 248, 180)
        };
        panel.Controls.Add(editor);
        panel.MouseDown += NoteForm_MouseDown;

        hideToolbarTimer = new System.Windows.Forms.Timer { Interval = 200 };
        hideToolbarTimer.Tick += (_, __) => 
        {
            hideToolbarTimer.Stop();
            var screenPos = Control.MousePosition;
            var formPos = PointToClient(screenPos);
            if (!ClientRectangle.Contains(formPos))
            {
                HideToolbar();
            }
        };

        var container = new BorderPanel(drawAll: true)
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(255, 255, 248, 180),
            Padding = new Padding(1)
        };
        var resizeHandle = new Panel
        {
            Name = "resizeHandle",
            Size = new Size(30, 30),
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Cursor = Cursors.SizeNWSE
        };
        
        resizeIcon = new Label
        {
            Name = "resizeIcon",
            Size = new Size(30, 30),
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ImageAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.SizeNWSE,
            Visible = false
        };
        
        try
        {
            var iconBitmap = LoadSvgAsBitmap("assests/icons/arrow-down-right.svg", 20, 20, Color.FromArgb(180, 180, 180));
            resizeIcon.Image = iconBitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading resize icon: {ex}");
        }
        
        resizeHandle.Controls.Add(resizeIcon);
        
        resizeIcon.MouseDown += (sender, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                isResizing = true;
                resizeStartPos = resizeHandle.PointToScreen(e.Location);
                resizeStartSize = Size;
            }
        };
        
        resizeHandle.MouseDown += (sender, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                isResizing = true;
                var handle = sender as Control;
                if (handle != null)
                {
                    resizeStartPos = handle.PointToScreen(e.Location);
                }
                else
                {
                    resizeStartPos = PointToScreen(e.Location);
                }
                resizeStartSize = Size;
            }
        };
        resizeIcon.MouseMove += (sender, e) =>
        {
            if (isResizing && e.Button == MouseButtons.Left)
            {
                var screenPos = resizeHandle.PointToScreen(e.Location);
                var deltaX = screenPos.X - resizeStartPos.X;
                var deltaY = screenPos.Y - resizeStartPos.Y;
                var newWidth = resizeStartSize.Width + deltaX;
                var newHeight = resizeStartSize.Height + deltaY;
                
                if (newWidth < MinimumSize.Width) newWidth = MinimumSize.Width;
                if (newHeight < MinimumSize.Height) newHeight = MinimumSize.Height;
                
                Size = new Size(newWidth, newHeight);
            }
        };
        
        resizeHandle.MouseMove += (sender, e) =>
        {
            if (isResizing && e.Button == MouseButtons.Left)
            {
                var handle = sender as Control;
                Point screenPos;
                if (handle != null)
                {
                    screenPos = handle.PointToScreen(e.Location);
                }
                else
                {
                    screenPos = PointToScreen(e.Location);
                }
                var deltaX = screenPos.X - resizeStartPos.X;
                var deltaY = screenPos.Y - resizeStartPos.Y;
                var newWidth = resizeStartSize.Width + deltaX;
                var newHeight = resizeStartSize.Height + deltaY;
                
                if (newWidth < MinimumSize.Width) newWidth = MinimumSize.Width;
                if (newHeight < MinimumSize.Height) newHeight = MinimumSize.Height;
                
                Size = new Size(newWidth, newHeight);
            }
        };
        resizeIcon.MouseUp += (sender, e) =>
        {
            if (e.Button == MouseButtons.Left && isResizing)
            {
                isResizing = false;
            }
        };
        
        resizeHandle.MouseUp += (sender, e) =>
        {
            if (e.Button == MouseButtons.Left && isResizing)
            {
                isResizing = false;
            }
        };

        container.Controls.Add(toolbar);
        container.Controls.Add(formatToolbar);
        container.Controls.Add(panel);
        container.MouseDown += NoteForm_MouseDown;
        container.MouseMove += Container_MouseMove;
        container.MouseEnter += (_, __) => 
        {
            hideToolbarTimer.Stop();
            ShowToolbar();
        };
        container.MouseLeave += (_, __) => 
        {
            hideToolbarTimer.Start();
            Cursor = Cursors.Default;
        };
        Controls.Add(container);
        Controls.Add(resizeHandle);
        resizeHandle.BringToFront();
        
        MouseMove += NoteForm_MouseMove;
        MouseUp += NoteForm_MouseUp;
        
        toolbar.MouseEnter += (_, __) => 
        {
            hideToolbarTimer.Stop();
            ShowToolbar();
        };
        toolbar.MouseLeave += (_, __) => hideToolbarTimer.Start();
        toolbar.MouseMove += (sender, e) =>
        {
            var toolbarControl = sender as Control;
            if (toolbarControl == null) return;
            var formPoint = toolbarControl.PointToScreen(e.Location);
            var clientPoint = PointToClient(formPoint);
            Cursor = IsInResizeArea(clientPoint) ? Cursors.SizeNWSE : Cursors.Default;
        };
        panel.MouseEnter += (_, __) => 
        {
            hideToolbarTimer.Stop();
            ShowToolbar();
        };
        panel.MouseLeave += (_, __) => hideToolbarTimer.Start();
        panel.MouseMove += (sender, e) =>
        {
            var panelControl = sender as Control;
            if (panelControl == null) return;
            var formPoint = panelControl.PointToScreen(e.Location);
            var clientPoint = PointToClient(formPoint);
            if (!isResizing)
            {
                Cursor = IsInResizeArea(clientPoint) ? Cursors.SizeNWSE : Cursors.Default;
            }
        };

        formatToolbar.MouseMove += (sender, e) =>
        {
            var toolbarControl = sender as Control;
            if (toolbarControl == null) return;
            var formPoint = toolbarControl.PointToScreen(e.Location);
            var clientPoint = PointToClient(formPoint);
            Cursor = IsInResizeArea(clientPoint) ? Cursors.SizeNWSE : Cursors.Default;
        };

        dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes", "data.json");
        LoadState(state);

        autosaveTimer = new System.Windows.Forms.Timer { Interval = 800 }; // debounce
        autosaveTimer.Tick += (_, __) => { autosaveTimer.Stop(); SaveState(); };
        editor.TextChanged += (_, __) => 
        { 
            autosaveTimer.Start();
            NoteTextChanged?.Invoke(this, EventArgs.Empty);
        };
        
        editor.KeyDown += Editor_KeyDown;

        MouseDown += NoteForm_MouseDown;
        MouseEnter += (_, __) => 
        {
            hideToolbarTimer.Stop();
            ShowToolbar();
        };
        MouseLeave += (_, __) => 
        {
            hideToolbarTimer.Start();
            if (!isResizing)
            {
                Cursor = Cursors.Default;
            }
        };

        Resize += (_, __) => 
        {
            resizeHandle.Location = new Point(Width - resizeHandle.Width - 1, Height - resizeHandle.Height - 1);
            UpdateButtonPositions();
            UpdateFormatButtonPositions();
        };
        
        Load += (_, __) => 
        {
            resizeHandle.Location = new Point(Width - resizeHandle.Width - 1, Height - resizeHandle.Height - 1);
            UpdateButtonPositions();
            UpdateFormatButtonPositions();
        };
        
        UpdateButtonPositions();
        UpdateFormatButtonPositions();
        resizeHandle.Location = new Point(Width - resizeHandle.Width - 1, Height - resizeHandle.Height - 1);
    }


    void UpdateButtonPositions()
    {
        var toolbar = closeButton.Parent;
        if (toolbar != null && toolbar.Width > 0)
        {
            const int buttonSize = 30;
            const int buttonPadding = 5;
            int buttonTop = (toolbar.Height - buttonSize) / 2;
            
            deleteButton.Location = new Point(toolbar.Width - buttonSize - buttonPadding, buttonTop);
            closeButton.Location = new Point(toolbar.Width - (buttonSize * 2) - (buttonPadding * 2), buttonTop);
            
            addButton.Location = new Point(buttonPadding, buttonTop);
        }
    }

    Label CreateFormatButton(string svgResourceName, Action clickAction)
    {
        var button = new Label
        {
            Size = new Size(28, 28),
            Anchor = AnchorStyles.None,
            BackColor = Color.FromArgb(255, 255, 248, 180),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Cursor = Cursors.Hand,
            ImageAlign = ContentAlignment.MiddleCenter
        };
        
        try
        {
            var bitmap = LoadSvgAsBitmap(svgResourceName, 20, 20, Color.FromArgb(100, 100, 100));
            button.Image = bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading SVG {svgResourceName}: {ex}");
        }
        
        button.Click += (_, __) => clickAction();
        button.MouseEnter += (_, __) => 
        { 
            button.BackColor = Color.FromArgb(255, 255, 200);
            try
            {
                var blackBitmap = LoadSvgAsBitmap(svgResourceName, 20, 20, Color.Black);
                button.Image?.Dispose();
                button.Image = blackBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating SVG color on hover: {ex}");
            }
        };
        button.MouseLeave += (_, __) => 
        { 
            button.BackColor = Color.FromArgb(255, 255, 248, 180);
            try
            {
                var grayBitmap = LoadSvgAsBitmap(svgResourceName, 20, 20, Color.FromArgb(100, 100, 100));
                button.Image?.Dispose();
                button.Image = grayBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating SVG color on leave: {ex}");
            }
        };
        
        return button;
    }

    Bitmap LoadSvgAsBitmap(string svgResourceName, int width, int height, Color color)
    {
        try
        {
            var svgDocument = ResourceHelper.LoadSvgFromResource(svgResourceName);
            if (svgDocument == null)
            {
                return new Bitmap(width, height);
            }
            
            var scale = 2.0f;
            var scaledWidth = (int)(width * scale);
            var scaledHeight = (int)(height * scale);
            
            svgDocument.Width = new SvgUnit(scaledWidth);
            svgDocument.Height = new SvgUnit(scaledHeight);
            
            SetSvgColor(svgDocument, color);
            
            var bitmap = new Bitmap(scaledWidth, scaledHeight);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                svgDocument.Draw(graphics);
            }
            
            var resized = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(bitmap, 0, 0, width, height);
            }
            
            bitmap.Dispose();
            return resized;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading SVG: {ex}");
            return new Bitmap(width, height);
        }
    }

    void SetSvgColor(SvgDocument document, Color color)
    {
        try
        {
            foreach (var element in document.Descendants().OfType<SvgElement>())
            {
                var fillStr = element.Fill?.ToString() ?? "";
                var strokeStr = element.Stroke?.ToString() ?? "";
                
                if (fillStr.Contains("none") || fillStr.Contains("transparent") || element.Fill == null)
                {
                    if (strokeStr.Contains("currentColor") || strokeStr.Contains("none") || element.Stroke == null)
                    {
                        element.Stroke = new SvgColourServer(color);
                    }
                }
                else if (fillStr.Contains("currentColor"))
                {
                    element.Fill = new SvgColourServer(color);
                }
                
                if (strokeStr.Contains("currentColor"))
                {
                    element.Stroke = new SvgColourServer(color);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting SVG color: {ex}");
        }
    }

    void UpdateFormatButtonPositions()
    {
        var toolbar = boldButton.Parent;
        if (toolbar != null && toolbar.Width > 0)
        {
            const int buttonSize = 28;
            const int buttonSpacing = 5;
            int startX = 10;
            int buttonTop = (toolbar.Height - buttonSize) / 2;
            
            boldButton.Location = new Point(startX, buttonTop);
            italicButton.Location = new Point(startX + buttonSize + buttonSpacing, buttonTop);
            underlineButton.Location = new Point(startX + (buttonSize + buttonSpacing) * 2, buttonTop);
            strikethroughButton.Location = new Point(startX + (buttonSize + buttonSpacing) * 3, buttonTop);
            removeFormattingButton.Location = new Point(startX + (buttonSize + buttonSpacing) * 4, buttonTop);
            listButton.Location = new Point(startX + (buttonSize + buttonSpacing) * 5, buttonTop);
            listOrderedButton.Location = new Point(startX + (buttonSize + buttonSpacing) * 6, buttonTop);
            clipboardButton.Location = new Point(startX + (buttonSize + buttonSpacing) * 7, buttonTop);
        }
    }

    void ToggleBold()
    {
        editor.Focus();
        if (editor.SelectionLength > 0)
        {
            var currentFont = editor.SelectionFont ?? editor.Font;
            var newStyle = currentFont.Style;
            if (currentFont.Bold)
            {
                newStyle &= ~FontStyle.Bold;
            }
            else
            {
                newStyle |= FontStyle.Bold;
            }
            editor.SelectionFont = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
        }
    }

    void ToggleItalic()
    {
        editor.Focus();
        if (editor.SelectionLength > 0)
        {
            var currentFont = editor.SelectionFont ?? editor.Font;
            var newStyle = currentFont.Style;
            if (currentFont.Italic)
            {
                newStyle &= ~FontStyle.Italic;
            }
            else
            {
                newStyle |= FontStyle.Italic;
            }
            editor.SelectionFont = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
        }
    }

    void ToggleUnderline()
    {
        editor.Focus();
        if (editor.SelectionLength > 0)
        {
            var currentFont = editor.SelectionFont ?? editor.Font;
            var newStyle = currentFont.Style;
            if (currentFont.Underline)
            {
                newStyle &= ~FontStyle.Underline;
            }
            else
            {
                newStyle |= FontStyle.Underline;
            }
            editor.SelectionFont = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
        }
    }

    void ToggleStrikethrough()
    {
        editor.Focus();
        if (editor.SelectionLength > 0)
        {
            var currentFont = editor.SelectionFont ?? editor.Font;
            var newStyle = currentFont.Style;
            if (currentFont.Strikeout)
            {
                newStyle &= ~FontStyle.Strikeout;
            }
            else
            {
                newStyle |= FontStyle.Strikeout;
            }
            editor.SelectionFont = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
        }
    }

    void InsertList()
    {
        editor.Focus();
        var selectionStart = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;
        
        if (selectionLength > 0)
        {
            var selectedText = editor.SelectedText;
            var lines = selectedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // Check if all non-empty lines already have bullet markers
            bool hasBullets = lines.All(line => 
            {
                if (string.IsNullOrWhiteSpace(line)) return true;
                var trimmed = line.TrimStart();
                return trimmed.StartsWith("• ");
            });
            
            var listText = string.Join("\r\n", lines.Select(line => 
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return "";
                }
                
                if (hasBullets)
                {
                    // Remove bullet marker
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("• "))
                    {
                        var indent = line.Length - line.TrimStart().Length;
                        var indentStr = indent > 0 ? line.Substring(0, indent) : "";
                        return indentStr + trimmed.Substring(2);
                    }
                    return line;
                }
                else
                {
                    // Add bullet marker
                    var indent = line.Length - line.TrimStart().Length;
                    var indentStr = indent > 0 ? line.Substring(0, indent) : "";
                    return indentStr + "• " + line.Trim();
                }
            }));
            
            editor.SelectedText = listText;
            editor.SelectionStart = selectionStart;
            editor.SelectionLength = listText.Length;
        }
        else
        {
            var cursorPos = editor.SelectionStart;
            var text = editor.Text;
            
            if (cursorPos < 0 || cursorPos > text.Length) return;
            
            var lineIndex = editor.GetLineFromCharIndex(cursorPos);
            var lineStart = editor.GetFirstCharIndexFromLine(lineIndex);
            if (lineStart < 0) return;
            
            var lineEnd = lineStart;
            while (lineEnd < text.Length && text[lineEnd] != '\n' && text[lineEnd] != '\r')
            {
                lineEnd++;
            }
            
            if (lineStart >= text.Length) return;
            
            var lineLength = lineEnd - lineStart;
            var currentLine = text.Substring(lineStart, lineLength);
            var trimmedLine = currentLine.TrimStart();
            
            if (trimmedLine.StartsWith("• "))
            {
                // Remove bullet marker
                var indent = currentLine.Length - currentLine.TrimStart().Length;
                var indentStr = indent > 0 ? currentLine.Substring(0, indent) : "";
                var newLine = indentStr + trimmedLine.Substring(2);
                
                editor.SelectionStart = lineStart;
                editor.SelectionLength = lineLength;
                editor.SelectedText = newLine;
                editor.SelectionStart = lineStart + indentStr.Length;
            }
            else
            {
                // Add bullet marker
                var indent = currentLine.Length - currentLine.TrimStart().Length;
                var indentStr = indent > 0 ? currentLine.Substring(0, indent) : "";
                var newLine = indentStr + "• " + currentLine.TrimStart();
                
                editor.SelectionStart = lineStart;
                editor.SelectionLength = lineLength;
                editor.SelectedText = newLine;
                editor.SelectionStart = lineStart + indentStr.Length + 2;
            }
        }
    }

    void InsertOrderedList()
    {
        editor.Focus();
        var selectionStart = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;
        
        if (selectionLength > 0)
        {
            var selectedText = editor.SelectedText;
            var lines = selectedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // Check if all non-empty lines already have numbering
            bool hasNumbering = lines.All(line => 
            {
                if (string.IsNullOrWhiteSpace(line)) return true;
                var trimmed = line.TrimStart();
                return Regex.IsMatch(trimmed, @"^\d+\. ");
            });
            
            int lineNumber = 1;
            var listText = string.Join("\r\n", lines.Select(line => 
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return "";
                }
                
                if (hasNumbering)
                {
                    // Remove numbering
                    var trimmed = line.TrimStart();
                    var match = Regex.Match(trimmed, @"^(\d+)\. ");
                    if (match.Success)
                    {
                        var indent = line.Length - line.TrimStart().Length;
                        var indentStr = indent > 0 ? line.Substring(0, indent) : "";
                        return indentStr + trimmed.Substring(match.Length);
                    }
                    return line;
                }
                else
                {
                    // Add numbering
                    var indent = line.Length - line.TrimStart().Length;
                    var indentStr = indent > 0 ? line.Substring(0, indent) : "";
                    return indentStr + $"{lineNumber++}. " + line.Trim();
                }
            }));
            
            editor.SelectedText = listText;
            editor.SelectionStart = selectionStart;
            editor.SelectionLength = listText.Length;
        }
        else
        {
            var cursorPos = editor.SelectionStart;
            var text = editor.Text;
            
            if (cursorPos < 0 || cursorPos > text.Length) return;
            
            var lineIndex = editor.GetLineFromCharIndex(cursorPos);
            var lineStart = editor.GetFirstCharIndexFromLine(lineIndex);
            if (lineStart < 0) return;
            
            var lineEnd = lineStart;
            while (lineEnd < text.Length && text[lineEnd] != '\n' && text[lineEnd] != '\r')
            {
                lineEnd++;
            }
            
            if (lineStart >= text.Length) return;
            
            var lineLength = lineEnd - lineStart;
            var currentLine = text.Substring(lineStart, lineLength);
            var trimmedLine = currentLine.TrimStart();
            
            var numberedMatch = Regex.Match(trimmedLine, @"^(\d+)\. ");
            if (numberedMatch.Success)
            {
                // Remove numbering
                var indent = currentLine.Length - currentLine.TrimStart().Length;
                var indentStr = indent > 0 ? currentLine.Substring(0, indent) : "";
                var newLine = indentStr + trimmedLine.Substring(numberedMatch.Length);
                
                editor.SelectionStart = lineStart;
                editor.SelectionLength = lineLength;
                editor.SelectedText = newLine;
                editor.SelectionStart = lineStart + indentStr.Length;
            }
            else
            {
                // Add numbering
                var indent = currentLine.Length - currentLine.TrimStart().Length;
                var indentStr = indent > 0 ? currentLine.Substring(0, indent) : "";
                var newLine = indentStr + "1. " + currentLine.TrimStart();
                
                editor.SelectionStart = lineStart;
                editor.SelectionLength = lineLength;
                editor.SelectedText = newLine;
                editor.SelectionStart = lineStart + indentStr.Length + 3;
            }
        }
    }

    void ClearFormatting()
    {
        editor.Focus();
        if (editor.SelectionLength > 0)
        {
            var currentFont = editor.SelectionFont ?? editor.Font;
            var defaultFont = new Font(currentFont.FontFamily, currentFont.Size, FontStyle.Regular);
            editor.SelectionFont = defaultFont;
        }
    }

    void CopyToClipboard()
    {
        editor.Focus();
        if (editor.SelectionLength > 0)
        {
            editor.Copy();
        }
        else
        {
            Clipboard.SetText(editor.Text);
        }
        
        SetClipboardIconToCheck();
        
        clipboardIconTimer?.Stop();
        clipboardIconTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        clipboardIconTimer.Tick += (_, __) => 
        {
            clipboardIconTimer.Stop();
            SetClipboardIconToNormal();
        };
        clipboardIconTimer.Start();
    }

    void SetClipboardIconToCheck()
    {
        try
        {
            isClipboardCheckIcon = true;
            var checkBitmap = LoadSvgAsBitmap("assests/icons/clipboard-check.svg", 20, 20, Color.FromArgb(100, 100, 100));
            clipboardButton.Image?.Dispose();
            clipboardButton.Image = checkBitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting clipboard check icon: {ex}");
        }
    }

    void SetClipboardIconToNormal()
    {
        try
        {
            isClipboardCheckIcon = false;
            var normalBitmap = LoadSvgAsBitmap("assests/icons/clipboard.svg", 20, 20, Color.FromArgb(100, 100, 100));
            clipboardButton.Image?.Dispose();
            clipboardButton.Image = normalBitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting clipboard normal icon: {ex}");
        }
    }

    void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        
        var cursorPos = editor.SelectionStart;
        var text = editor.Text;
        
        if (cursorPos < 0 || cursorPos > text.Length) return;
        
        var lineIndex = editor.GetLineFromCharIndex(cursorPos);
        var lineStart = editor.GetFirstCharIndexFromLine(lineIndex);
        if (lineStart < 0) return;
        
        var lineEnd = cursorPos;
        while (lineEnd < text.Length && text[lineEnd] != '\n' && text[lineEnd] != '\r')
        {
            lineEnd++;
        }
        
        if (lineStart >= text.Length) return;
        
        var lineLength = Math.Min(lineEnd - lineStart, text.Length - lineStart);
        var currentLine = text.Substring(lineStart, lineLength);
        var trimmedLine = currentLine.TrimStart();
        
        if (trimmedLine.StartsWith("• "))
        {
            e.Handled = true;
            
            var indent = currentLine.Length - currentLine.TrimStart().Length;
            var indentStr = indent > 0 ? currentLine.Substring(0, indent) : "";
            
            editor.SelectedText = "\r\n" + indentStr + "• ";
            editor.SelectionStart = editor.SelectionStart;
        }
        else
        {
            var numberedMatch = Regex.Match(trimmedLine, @"^(\d+)\. ");
            if (numberedMatch.Success && int.TryParse(numberedMatch.Groups[1].Value, out int currentNumber))
            {
                e.Handled = true;
                
                var indent = currentLine.Length - currentLine.TrimStart().Length;
                var indentStr = indent > 0 ? currentLine.Substring(0, indent) : "";
                
                var nextNumber = currentNumber + 1;
                editor.SelectedText = "\r\n" + indentStr + nextNumber + ". ";
                editor.SelectionStart = editor.SelectionStart;
            }
        }
    }

    void ShowToolbar()
    {
        var toolbar = closeButton.Parent;
        if (toolbar != null && toolbar.Height == 0)
        {
            toolbar.Height = 30;
            UpdateButtonPositions();
        }
        
        if (formatToolbar != null && formatToolbar.Height == 0)
        {
            formatToolbar.Height = 30;
            UpdateFormatButtonPositions();
        }
        
        if (resizeIcon != null && !resizeIcon.Visible)
        {
            resizeIcon.Visible = true;
        }
    }

    void HideToolbar()
    {
        var toolbar = closeButton.Parent;
        if (toolbar != null && toolbar.Height > 0)
        {
            toolbar.Height = 0;
        }
        
        if (formatToolbar != null && formatToolbar.Height > 0)
        {
            formatToolbar.Height = 0;
        }
        
        if (resizeIcon != null && resizeIcon.Visible)
        {
            resizeIcon.Visible = false;
        }
    }

    bool IsInResizeArea(Point point)
    {
        const int resizeAreaSize = 30;
        return point.X >= Width - resizeAreaSize && point.Y >= Height - resizeAreaSize;
    }

    void Container_MouseMove(object? sender, MouseEventArgs e)
    {
        var container = sender as Control;
        if (container == null) return;
        
        var formPoint = container.PointToScreen(e.Location);
        var clientPoint = PointToClient(formPoint);
        
        if (IsInResizeArea(clientPoint))
        {
            Cursor = Cursors.SizeNWSE;
            if (e.Button == MouseButtons.Left && !isResizing)
            {
                isResizing = true;
                resizeStartPos = formPoint;
                resizeStartSize = Size;
            }
        }
        else if (!isResizing)
        {
            Cursor = Cursors.Default;
        }
        
        if (isResizing && e.Button == MouseButtons.Left)
        {
            var deltaX = formPoint.X - resizeStartPos.X;
            var deltaY = formPoint.Y - resizeStartPos.Y;
            var newWidth = resizeStartSize.Width + deltaX;
            var newHeight = resizeStartSize.Height + deltaY;
            
            if (newWidth < MinimumSize.Width) newWidth = MinimumSize.Width;
            if (newHeight < MinimumSize.Height) newHeight = MinimumSize.Height;
            
            Size = new Size(newWidth, newHeight);
        }
    }

    void NoteForm_MouseMove(object? sender, MouseEventArgs e)
    {
        if (isResizing)
        {
            var screenPos = PointToScreen(e.Location);
            var deltaX = screenPos.X - resizeStartPos.X;
            var deltaY = screenPos.Y - resizeStartPos.Y;
            var newWidth = resizeStartSize.Width + deltaX;
            var newHeight = resizeStartSize.Height + deltaY;
            
            if (newWidth < MinimumSize.Width) newWidth = MinimumSize.Width;
            if (newHeight < MinimumSize.Height) newHeight = MinimumSize.Height;
            
            Size = new Size(newWidth, newHeight);
        }
        else if (IsInResizeArea(e.Location))
        {
            Cursor = Cursors.SizeNWSE;
        }
        else
        {
            Cursor = Cursors.Default;
        }
    }

    void NoteForm_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && isResizing)
        {
            isResizing = false;
            Cursor = Cursors.Default;
        }
    }

    void NoteForm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (IsInResizeArea(e.Location))
            {
                isResizing = true;
                resizeStartPos = PointToScreen(e.Location);
                resizeStartSize = Size;
                return;
            }
            
            var control = GetChildAtPoint(e.Location);
            
            if (control == addButton || control == closeButton || control == deleteButton || 
                control == boldButton || control == italicButton || control == underlineButton || 
                control == strikethroughButton || control == listButton || control == listOrderedButton || 
                control == removeFormattingButton || control == clipboardButton || control == editor)
            {
                return;
            }
            
            Control? current = control;
            while (current != null && current != this)
            {
                if (current == editor || current.Name == "resizeHandle" || (current is Label && current.Parent != null && current.Parent.Name == "resizeHandle"))
                {
                    return;
                }
                current = current.Parent;
            }
            
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (int)Keys.N); // Ctrl+Alt+N
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            UnregisterHotKey(Handle, HOTKEY_ID);
        }
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            if (Visible) Hide(); else { Show(); Activate(); }
        }
        base.WndProc(ref m);
    }

    void LoadState(NoteState state)
    {
        if (!string.IsNullOrEmpty(state.Text)) editor.Text = state.Text;
        if (state.Width > 0 && state.Height > 0)
        {
            Bounds = new Rectangle(state.X, state.Y, state.Width, state.Height);
        }
    }

    public NoteState GetState()
    {
        return new NoteState
        {
            Id = noteId,
            Text = editor.Text,
            X = Bounds.X,
            Y = Bounds.Y,
            Width = Bounds.Width,
            Height = Bounds.Height
        };
    }

    public string GetText()
    {
        return editor.Text;
    }

    public void SaveState()
    {
        app?.SaveAllStatesRequested();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }
}

public class NoteState
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 360;
    public int Height { get; set; } = 320;
}

public class AppSettings
{
    public bool HideTaskbarIcon { get; set; }
    public bool TopMost { get; set; }
    public bool? RunAtStartup { get; set; }
    public bool? ConfirmDelete { get; set; }
}

public class NoteStateCollection
{
    public List<NoteState> Notes { get; set; } = new List<NoteState>();
    public AppSettings? Settings { get; set; }

    public static NoteStateCollection Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var collection = JsonSerializer.Deserialize<NoteStateCollection>(json);
                if (collection != null) return collection;
            }
        }
        catch { }
        return new NoteStateCollection { Notes = new List<NoteState>() };
    }

    public static void Save(string path, List<NoteState> states, AppSettings? settings = null)
    {
        try
        {
            var collection = new NoteStateCollection { Notes = states, Settings = settings };
            var json = JsonSerializer.Serialize(collection, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch { }
    }
}
