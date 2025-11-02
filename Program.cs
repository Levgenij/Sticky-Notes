using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text; 
using System.Windows.Forms;
using System.Drawing;

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

public class StickyApp : ApplicationContext
{

    readonly List<NoteForm> notes;
    readonly NotifyIcon tray;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem showHideItem = null!;
    readonly ToolStripMenuItem topMostItem = null!;
    readonly ToolStripMenuItem hideTaskbarItem = null!;
    readonly ToolStripMenuItem notesMenuItem = null!;
    readonly string dataPath;
    readonly Icon? customIcon;

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

        notesMenuItem = new ToolStripMenuItem("Notes");
        notesMenuItem.DropDownItems.Clear();

        var newNoteItem = new ToolStripMenuItem("New note");
        newNoteItem.Click += (_, __) => CreateNewNote();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, __) => ExitApp();

        menu.Items.AddRange(new ToolStripItem[] { showHideItem, topMostItem, hideTaskbarItem, new ToolStripSeparator(), notesMenuItem, newNoteItem, new ToolStripSeparator(), exitItem });

        try
        {
            var trayIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tray.png");
            if (File.Exists(trayIconPath))
            {
                using (var srcBmp = new Bitmap(trayIconPath))
                using (var resized = new Bitmap(srcBmp, new Size(32, 32)))
                {
                    var hIcon = resized.GetHicon();
                    customIcon = Icon.FromHandle(hIcon);
                }
            }
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
        note.RequestNewNote += (_, __) => CreateNewNote();
        note.RequestDelete += (_, __) => DeleteNote(note);
        note.NoteTextChanged += (_, __) => UpdateNotesMenu();
        notes.Add(note);
        if (hideTaskbarItem != null)
        {
            note.ShowInTaskbar = !hideTaskbarItem.Checked;
        }
        note.Show();
        UpdateTopMostMenu();
        UpdateNotesMenu();
    }

    void CreateNewNote()
    {
        var state = new NoteState
        {
            Id = Guid.NewGuid().ToString(),
            X = notes.Count > 0 ? notes[0].Left + 30 : 100,
            Y = notes.Count > 0 ? notes[0].Top + 30 : 100
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
        note.SaveState();
        notes.Remove(note);
        note.Close();
        SaveAllStates();
        UpdateTrayText();
        UpdateNotesMenu();
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
            TopMost = topMostItem?.Checked ?? false
        };
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
        base.WndProc(ref m);
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
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
            if (File.Exists(iconPath))
            {
                using (var srcBmp = new Bitmap(iconPath))
                using (var resized = new Bitmap(srcBmp, new Size(32, 32)))
                {
                    var hIcon = resized.GetHicon();
                    Icon = Icon.FromHandle(hIcon);
                }
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

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 50, 10, 10),
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
            Size = new Size(30, 30),
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Cursor = Cursors.SizeNWSE
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
        resizeHandle.MouseUp += (sender, e) =>
        {
            if (e.Button == MouseButtons.Left && isResizing)
            {
                isResizing = false;
            }
        };

        container.Controls.Add(toolbar);
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

        dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes", "data.json");
        LoadState(state);

        autosaveTimer = new System.Windows.Forms.Timer { Interval = 800 }; // debounce
        autosaveTimer.Tick += (_, __) => { autosaveTimer.Stop(); SaveState(); };
        editor.TextChanged += (_, __) => 
        { 
            autosaveTimer.Start();
            NoteTextChanged?.Invoke(this, EventArgs.Empty);
        };

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
        };
        
        Load += (_, __) => 
        {
            resizeHandle.Location = new Point(Width - resizeHandle.Width - 1, Height - resizeHandle.Height - 1);
            UpdateButtonPositions();
        };
        
        UpdateButtonPositions();
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

    void ShowToolbar()
    {
        var toolbar = closeButton.Parent;
        if (toolbar != null && toolbar.Height == 0)
        {
            toolbar.Height = 30;
            UpdateButtonPositions();
        }
    }

    void HideToolbar()
    {
        var toolbar = closeButton.Parent;
        if (toolbar != null && toolbar.Height > 0)
        {
            toolbar.Height = 0;
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
            
            if (control == addButton || control == closeButton || control == deleteButton || control == editor)
            {
                return;
            }
            
            Control? current = control;
            while (current != null && current != this)
            {
                if (current == editor)
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
