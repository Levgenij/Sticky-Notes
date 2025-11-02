using System;
using System.IO;
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

    readonly NoteForm note;
    readonly NotifyIcon tray;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem showHideItem;
    readonly ToolStripMenuItem topMostItem;
    readonly string dataPath;
    readonly Icon? customIcon;

    public StickyApp()
    {
        dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes", "data.json");
        Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);

        var state = NoteState.Load(dataPath);

        note = new NoteForm(state);
        note.VisibleChanged += (_, __) => UpdateTrayText();
        note.FormClosed += (_, __) => ExitThread();
        note.RequestExit += (_, __) => ExitApp();

        menu = new ContextMenuStrip();
        showHideItem = new ToolStripMenuItem("Show");
        showHideItem.Click += (_, __) => ToggleNote();

        topMostItem = new ToolStripMenuItem("Always on top") { Checked = note.TopMost, CheckOnClick = true };
        topMostItem.CheckedChanged += (_, __) => note.TopMost = topMostItem.Checked;

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, __) => ExitApp();

        menu.Items.AddRange(new ToolStripItem[] { showHideItem, topMostItem, new ToolStripSeparator(), exitItem });

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
        tray.DoubleClick += (_, __) => ToggleNote();

        note.Show();
    }

    void ToggleNote()
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
    }

    void UpdateTrayText()
    {
        showHideItem.Text = note.Visible ? "Hide" : "Show";
        tray.Text = note.Visible ? "Sticky Notes — Visible" : "Sticky Notes — Hidden";
    }

    void ExitApp()
    {
        note.SaveState();
        tray.Visible = false;
        ExitThread();
    }
}

public class NoteForm : Form
{
    const int MOD_ALT = 0x0001;
    const int MOD_CONTROL = 0x0002;
    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 0x7777;

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    readonly RichTextBox editor;
    readonly System.Windows.Forms.Timer autosaveTimer;
    readonly string dataPath;
    public event EventHandler? RequestExit;

    public NoteForm(NoteState state)
    {
        Text = "Sticky Notes";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        TopMost = true;
        BackColor = Color.FromArgb(255, 255, 248, 180);
        MinimumSize = new Size(250, 180);

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

        editor = new RichTextBox
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

        Controls.Add(editor);

        dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes", "data.json");
        LoadState(state);

        autosaveTimer = new System.Windows.Forms.Timer { Interval = 800 }; // debounce
        autosaveTimer.Tick += (_, __) => { autosaveTimer.Stop(); SaveState(); };
        editor.TextChanged += (_, __) => autosaveTimer.Start();
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

    public void SaveState()
    {
        var s = new NoteState
        {
            Text = editor.Text,
            X = Bounds.X,
            Y = Bounds.Y,
            Width = Bounds.Width,
            Height = Bounds.Height
        };
        NoteState.Save(dataPath, s);
    }
}

public class NoteState
{
    public string Text { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 360;
    public int Height { get; set; } = 320;

    public static NoteState Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var s = JsonSerializer.Deserialize<NoteState>(json);
                if (s != null) return s;
            }
        }
        catch { }
        return new NoteState();
    }

    public static void Save(string path, NoteState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch { }
    }
}
