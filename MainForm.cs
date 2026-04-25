using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Drawing.Drawing2D;

namespace SimpleBrowser;

public class MainForm : Form
{
    // ─── Colors ───────────────────────────────────────────────────────────
    static readonly Color BgDark      = Color.FromArgb(24, 24, 28);
    static readonly Color BgPanel     = Color.FromArgb(32, 32, 38);
    static readonly Color BgTab       = Color.FromArgb(40, 40, 48);
    static readonly Color BgTabActive = Color.FromArgb(55, 55, 68);
    static readonly Color AccentBlue  = Color.FromArgb(88, 130, 255);
    static readonly Color TextColor   = Color.FromArgb(220, 220, 230);
    static readonly Color TextMuted   = Color.FromArgb(130, 130, 150);
    static readonly Color UrlBarBg    = Color.FromArgb(45, 45, 55);

    // ─── Controls ─────────────────────────────────────────────────────────
    private Panel       pnlNav       = null!;
    private Panel       pnlTabBar    = null!;
    private Panel       pnlStatus    = null!;
    private TextBox     txtUrl       = null!;
    private Label       lblStatus    = null!;
    private ProgressBar pbPage       = null!;
    private FlowLayoutPanel flpTabs  = null!;
    private Panel       pnlContent   = null!;

    // ─── Tab state ────────────────────────────────────────────────────────
    private readonly List<BrowserTab> _tabs = new();
    private BrowserTab? _activeTab;

    private const string HOME_URL = "https://www.baidu.com";

    // ─── Download panel ───────────────────────────────────────────────────
    private Panel?      pnlDownloads;
    private ListBox?    lstDownloads;
    private readonly List<string> _downloadLog = new();

    public MainForm()
    {
        InitUI();
        _ = AddNewTab(HOME_URL);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UI SETUP
    // ═══════════════════════════════════════════════════════════════════════
    private void InitUI()
    {
        Text            = "Simple Browser";
        Size            = new Size(1280, 800);
        MinimumSize     = new Size(640, 480);
        BackColor       = BgDark;
        ForeColor       = TextColor;
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9f, FontStyle.Regular);

        // Tab bar
        pnlTabBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 36,
            BackColor = BgPanel,
            Padding   = new Padding(4, 0, 4, 0)
        };

        flpTabs = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            AutoScroll    = false,
            BackColor     = BgPanel,
        };

        var btnNewTab = MakeIconButton("＋", "新建标签页");
        btnNewTab.Click += async (_, _) => await AddNewTab(HOME_URL);

        pnlTabBar.Controls.Add(flpTabs);
        pnlTabBar.Controls.Add(btnNewTab);
        btnNewTab.Dock = DockStyle.Right;

        // Navigation bar
        pnlNav = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 44,
            BackColor = BgPanel,
            Padding   = new Padding(6, 5, 6, 5)
        };

        var btnBack    = MakeIconButton("◀", "后退");
        var btnFwd     = MakeIconButton("▶", "前进");
        var btnRefresh = MakeIconButton("↺", "刷新");
        var btnHome    = MakeIconButton("⌂", "主页");
        var btnDl      = MakeIconButton("⬇", "下载管理");

        btnBack.Click    += (_, _) => _activeTab?.WebView.GoBack();
        btnFwd.Click     += (_, _) => _activeTab?.WebView.GoForward();
        btnRefresh.Click += (_, _) => OnRefreshClick();
        btnHome.Click    += async (_, _) =>
        {
            if (_activeTab != null) await _activeTab.WebView.EnsureCoreWebView2Async();
            _activeTab?.WebView.CoreWebView2?.Navigate(HOME_URL);
        };
        btnDl.Click += (_, _) => ToggleDownloadPanel();

        txtUrl = new TextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = UrlBarBg,
            ForeColor   = TextColor,
            BorderStyle = BorderStyle.None,
            Font        = new Font("Segoe UI", 11f),
        };
        txtUrl.KeyDown  += OnUrlKeyDown;
        txtUrl.Enter    += (_, _) => txtUrl.SelectAll();

        // Wrap url bar for padding / rounded look
        var pnlUrlWrap = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = UrlBarBg,
            Height    = 30,
            Margin    = new Padding(4, 0, 4, 0),
        };
        pnlUrlWrap.Padding = new Padding(8, 0, 8, 0);
        pnlUrlWrap.Controls.Add(txtUrl);
        txtUrl.Dock = DockStyle.Fill;

        var pnlNavButtons = new Panel
        {
            Dock      = DockStyle.Left,
            Width     = 148,
            BackColor = BgPanel,
        };
        btnBack.Dock    = DockStyle.Left;
        btnFwd.Dock     = DockStyle.Left;
        btnRefresh.Dock = DockStyle.Left;
        btnHome.Dock    = DockStyle.Left;
        pnlNavButtons.Controls.AddRange(new Control[] { btnHome, btnRefresh, btnFwd, btnBack });

        btnDl.Dock = DockStyle.Right;

        pnlNav.Controls.Add(pnlUrlWrap);
        pnlNav.Controls.Add(pnlNavButtons);
        pnlNav.Controls.Add(btnDl);

        // Status bar
        pnlStatus = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 22,
            BackColor = BgPanel,
            Padding   = new Padding(6, 2, 6, 0),
        };
        lblStatus = new Label
        {
            Dock      = DockStyle.Left,
            AutoSize  = false,
            Width     = 600,
            ForeColor = TextMuted,
            Font      = new Font("Segoe UI", 8f),
            TextAlign = ContentAlignment.MiddleLeft,
            Text      = "就绪",
        };
        pbPage = new ProgressBar
        {
            Dock    = DockStyle.Right,
            Width   = 120,
            Style   = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Value   = 0,
            Visible = false,
        };
        pnlStatus.Controls.Add(lblStatus);
        pnlStatus.Controls.Add(pbPage);

        // Content area
        pnlContent = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = BgDark,
        };

        Controls.Add(pnlContent);
        Controls.Add(pnlStatus);
        Controls.Add(pnlNav);
        Controls.Add(pnlTabBar);

        // Custom paint for rounded URL bar
        pnlUrlWrap.Paint += PaintRoundedPanel;
    }

    private void PaintRoundedPanel(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel pnl) return;
        e.Graphics.Clear(BgPanel);
        using var path = RoundedRect(pnl.ClientRectangle, 8);
        using var brush = new SolidBrush(UrlBarBg);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TAB MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════
    private async Task AddNewTab(string url)
    {
        var tab = new BrowserTab(url);

        // Build tab header button
        var header = new TabHeaderPanel(tab);
        header.CloseClicked += () => CloseTab(tab);
        header.Click         += (_, _) => ActivateTab(tab);

        tab.Header = header;
        _tabs.Add(tab);
        flpTabs.Controls.Add(header);

        // Init WebView
        pnlContent.Controls.Add(tab.WebView);
        tab.WebView.BringToFront();

        await tab.WebView.EnsureCoreWebView2Async();

        // Events
        tab.WebView.CoreWebView2.NavigationStarting  += (_, e) => OnTabNavStart(tab, e);
        tab.WebView.CoreWebView2.NavigationCompleted += (_, e) => OnTabNavComplete(tab, e);
        tab.WebView.CoreWebView2.DocumentTitleChanged += (_, _) => OnTabTitleChanged(tab);
        tab.WebView.CoreWebView2.DownloadStarting    += OnDownloadStarting;
        tab.WebView.CoreWebView2.NewWindowRequested  += OnNewWindowRequested;

        tab.WebView.CoreWebView2.Navigate(url);
        ActivateTab(tab);
    }

    private void ActivateTab(BrowserTab tab)
    {
        _activeTab = tab;
        foreach (var t in _tabs)
        {
            t.WebView.Visible = (t == tab);
            t.Header?.SetActive(t == tab);
        }
        txtUrl.Text = tab.CurrentUrl;
        lblStatus.Text = tab.Title;
    }

    private void CloseTab(BrowserTab tab)
    {
        if (_tabs.Count == 1)
        {
            // Just navigate home instead of closing last tab
            tab.WebView.CoreWebView2?.Navigate(HOME_URL);
            return;
        }
        int idx = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        flpTabs.Controls.Remove(tab.Header);
        pnlContent.Controls.Remove(tab.WebView);
        tab.WebView.Dispose();

        var next = _tabs[Math.Min(idx, _tabs.Count - 1)];
        ActivateTab(next);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NAVIGATION EVENTS
    // ═══════════════════════════════════════════════════════════════════════
    private void OnRefreshClick()
    {
        if (_activeTab?.WebView.CoreWebView2 == null) return;
        if (_activeTab.IsLoading)
            _activeTab.WebView.CoreWebView2.Stop();
        else
            _activeTab.WebView.Reload();
    }

    private void OnUrlKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        Navigate(txtUrl.Text.Trim());
    }

    private void Navigate(string input)
    {
        if (_activeTab?.WebView.CoreWebView2 == null) return;
        if (!input.StartsWith("http://") && !input.StartsWith("https://"))
        {
            if (input.Contains('.') && !input.Contains(' '))
                input = "https://" + input;
            else
                input = "https://www.baidu.com/s?wd=" + Uri.EscapeDataString(input);
        }
        _activeTab.WebView.CoreWebView2.Navigate(input);
    }

    private void OnTabNavStart(BrowserTab tab, CoreWebView2NavigationStartingEventArgs e)
    {
        tab.IsLoading   = true;
        tab.CurrentUrl  = e.Uri;
        if (tab == _activeTab)
        {
            Invoke(() =>
            {
                txtUrl.Text    = e.Uri;
                pbPage.Value   = 0;
                pbPage.Visible = true;
                lblStatus.Text = "正在加载… " + e.Uri;
            });
        }
        AnimateProgress(tab);
    }

    private void OnTabNavComplete(BrowserTab tab, CoreWebView2NavigationCompletedEventArgs e)
    {
        tab.IsLoading  = false;
        tab.CurrentUrl = tab.WebView.Source?.ToString() ?? tab.CurrentUrl;
        if (tab == _activeTab)
        {
            Invoke(() =>
            {
                txtUrl.Text    = tab.CurrentUrl;
                pbPage.Value   = 100;
                pbPage.Visible = false;
                lblStatus.Text = e.IsSuccess ? "就绪" : "加载失败";
            });
        }
    }

    private void OnTabTitleChanged(BrowserTab tab)
    {
        tab.Title = tab.WebView.CoreWebView2.DocumentTitle;
        Invoke(() =>
        {
            tab.Header?.UpdateTitle(tab.Title);
            if (tab == _activeTab)
            {
                Text = tab.Title + " – Simple Browser";
                lblStatus.Text = tab.Title;
            }
        });
    }

    private async void AnimateProgress(BrowserTab tab)
    {
        int val = 0;
        while (tab.IsLoading && val < 90)
        {
            val = Math.Min(val + Random.Shared.Next(2, 8), 90);
            if (tab == _activeTab)
                Invoke(() => { if (pbPage.Visible) pbPage.Value = val; });
            await Task.Delay(120);
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        _ = AddNewTab(e.Uri);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DOWNLOAD MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════
    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var op = e.DownloadOperation;
        string filename = Path.GetFileName(op.ResultFilePath);
        string logEntry = $"▼ {filename}";

        _downloadLog.Add(logEntry);
        Invoke(() =>
        {
            EnsureDownloadPanel();
            lstDownloads!.Items.Insert(0, logEntry);
            if (!pnlDownloads!.Visible) ShowDownloadPanel();
        });

        op.StateChanged += (_, _) =>
        {
            Invoke(() => UpdateDownloadEntry(op, logEntry));
        };
    }

    private void UpdateDownloadEntry(CoreWebView2DownloadOperation op, string original)
    {
        if (lstDownloads == null) return;
        int idx = lstDownloads.Items.IndexOf(original);
        if (idx < 0) return;

        string updated = op.State switch
        {
            CoreWebView2DownloadState.Completed  => $"✓ {Path.GetFileName(op.ResultFilePath)}",
            CoreWebView2DownloadState.Interrupted => $"✗ {Path.GetFileName(op.ResultFilePath)} (失败)",
            _ => $"▼ {Path.GetFileName(op.ResultFilePath)} — {FormatBytes(op.BytesReceived)}/{FormatBytes((long)(op.TotalBytesToReceive ?? 0L))}"
        };
        lstDownloads.Items[idx] = updated;
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024            => $"{bytes} B",
            < 1024 * 1024     => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} MB",
            _                 => $"{bytes / 1024.0 / 1024 / 1024:F2} GB"
        };
    }

    private void EnsureDownloadPanel()
    {
        if (pnlDownloads != null) return;

        pnlDownloads = new Panel
        {
            Width     = 320,
            Height    = 300,
            BackColor = BgPanel,
            Visible   = false,
            BorderStyle = BorderStyle.None,
        };
        pnlDownloads.Paint += (_, e) =>
        {
            using var pen = new Pen(AccentBlue, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlDownloads.Width - 1, pnlDownloads.Height - 1);
        };

        var lblTitle = new Label
        {
            Text      = "下载管理",
            Dock      = DockStyle.Top,
            Height    = 30,
            ForeColor = AccentBlue,
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = BgPanel,
        };
        var btnClear = new Button
        {
            Text      = "清空",
            Dock      = DockStyle.Bottom,
            Height    = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = BgTab,
            ForeColor = TextColor,
        };
        btnClear.FlatAppearance.BorderColor = AccentBlue;
        btnClear.Click += (_, _) => { lstDownloads!.Items.Clear(); _downloadLog.Clear(); };

        lstDownloads = new ListBox
        {
            Dock      = DockStyle.Fill,
            BackColor = BgDark,
            ForeColor = TextColor,
            BorderStyle = BorderStyle.None,
            Font      = new Font("Segoe UI", 8.5f),
        };

        pnlDownloads.Controls.Add(lstDownloads);
        pnlDownloads.Controls.Add(btnClear);
        pnlDownloads.Controls.Add(lblTitle);

        // Overlay on content area
        pnlContent.Controls.Add(pnlDownloads);
        pnlDownloads.BringToFront();
    }

    private void ShowDownloadPanel()
    {
        EnsureDownloadPanel();
        PositionDownloadPanel();
        pnlDownloads!.Visible = true;
        pnlDownloads.BringToFront();
    }

    private void PositionDownloadPanel()
    {
        if (pnlDownloads == null) return;
        pnlDownloads.Location = new Point(
            pnlContent.Width  - pnlDownloads.Width  - 8,
            8);
    }

    private void ToggleDownloadPanel()
    {
        EnsureDownloadPanel();
        if (pnlDownloads!.Visible)
            pnlDownloads.Visible = false;
        else
            ShowDownloadPanel();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PositionDownloadPanel();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════
    private static Button MakeIconButton(string icon, string tip)
    {
        var btn = new Button
        {
            Text      = icon,
            Width     = 36,
            Height    = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = TextColor,
            Font      = new Font("Segoe UI", 12f),
            Cursor    = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize      = 0;
        btn.FlatAppearance.MouseOverBackColor  = BgTabActive;
        btn.FlatAppearance.MouseDownBackColor  = BgTab;
        var tt = new ToolTip();
        tt.SetToolTip(btn, tip);
        return btn;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// BrowserTab — holds WebView2 + metadata
// ═══════════════════════════════════════════════════════════════════════════
class BrowserTab
{
    public WebView2         WebView    { get; }
    public string           CurrentUrl { get; set; }
    public string           Title      { get; set; }
    public bool             IsLoading  { get; set; }
    public TabHeaderPanel?  Header     { get; set; }

    public BrowserTab(string url)
    {
        CurrentUrl = url;
        Title      = "新标签页";
        WebView    = new WebView2
        {
            Dock = DockStyle.Fill,
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// TabHeaderPanel — a custom drawn tab button
// ═══════════════════════════════════════════════════════════════════════════
class TabHeaderPanel : Panel
{
    static readonly Color ActiveBg  = Color.FromArgb(55, 55, 68);
    static readonly Color NormalBg  = Color.FromArgb(38, 38, 48);
    static readonly Color TextColor = Color.FromArgb(210, 210, 225);
    static readonly Color MutedColor= Color.FromArgb(120, 120, 140);
    static readonly Color CloseHover= Color.FromArgb(200, 70, 70);

    private readonly Label   _label;
    private readonly Button  _btnClose;
    private bool _active;

    public event Action? CloseClicked;

    public TabHeaderPanel(BrowserTab tab)
    {
        Width       = 180;
        Height      = 36;
        BackColor   = NormalBg;
        Margin      = new Padding(1, 3, 0, 0);
        Cursor      = Cursors.Hand;

        _label = new Label
        {
            Text      = tab.Title,
            ForeColor = TextColor,
            Font      = new Font("Segoe UI", 8.5f),
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds    = new Rectangle(8, 0, 140, 36),
        };

        _btnClose = new Button
        {
            Text      = "✕",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = MutedColor,
            Font      = new Font("Segoe UI", 8f),
            Bounds    = new Rectangle(152, 8, 20, 20),
            Cursor    = Cursors.Hand,
        };
        _btnClose.FlatAppearance.BorderSize = 0;
        _btnClose.FlatAppearance.MouseOverBackColor = CloseHover;
        _btnClose.Click += (_, _) => CloseClicked?.Invoke();


        Controls.Add(_label);
        Controls.Add(_btnClose);

        // Forward click on label to panel
        _label.Click += (_, e) => OnClick(e);
    }

    public void SetActive(bool active)
    {
        _active   = active;
        BackColor = active ? ActiveBg : NormalBg;
        _label.ForeColor = active ? TextColor : MutedColor;
    }

    public void UpdateTitle(string title)
    {
        _label.Text = string.IsNullOrEmpty(title) ? "…" :
                      title.Length > 22 ? title[..22] + "…" : title;
    }
}
