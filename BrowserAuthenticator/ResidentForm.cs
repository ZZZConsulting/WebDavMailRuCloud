using System.ComponentModel;
using System.Configuration;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace BrowserAuthenticator;

public partial class ResidentForm : Form
{
    public static readonly string[] YandexDomains = { "yandex", "ya" };
    public static readonly string[] MailDomains = { "mail", "inbox", "bk", "list", "vk", "internet" };

    private HttpListener? Listener;
    private bool RunServer = false;
    private string PreviousPort;

    public delegate void Execute(string login, BrowserAppResult response, Dictionary<string, string> headers);

    public Execute AuthExecuteDelegate;
    private readonly int? SavedTop = null;
    private readonly int? SavedLeft = null;
    private SemaphoreSlim _showBrowserLocker;
    private bool _doNotSave = false;
    private bool _stopUI = false;

    private int AuthenticationOkCounter = 0;
    private int AuthenticationFailCounter = 0;


    public ResidentForm()
    {
        InitializeComponent();

        _showBrowserLocker = new SemaphoreSlim(1, 1);

        var screen = Screen.GetWorkingArea(this);
        Top = screen.Height + 100;
        ShowInTaskbar = false;

        NotifyIcon.Visible = true;

        // Get the current configuration file.
        Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        _doNotSave = true;
        string? value = config.AppSettings?.Settings?["port"]?.Value;
        if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value, out _))
            Port.Text = value;
#if DEBUG
        else
            Port.Text = "54322";
#endif

        value = config.AppSettings?.Settings?["password"]?.Value;
        if (!string.IsNullOrWhiteSpace(value))
            Password.Text = value;
#if DEBUG
        else
            Password.Text = "240C8CFC-64E6-4329-A1B3-D6BA2CEC53CB";
#endif
        TestLogin.BeginUpdate();
        value = config.AppSettings?.Settings?["logins"]?.Value ?? string.Empty;
        string[] logins = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var s in logins)
        {
            TestLogin.Items.Add(s);
        }
        TestLogin.EndUpdate();

        value = config.AppSettings?.Settings?["Top"]?.Value;
        if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value, out int top))
            SavedTop = top;
        value = config.AppSettings?.Settings?["Left"]?.Value;
        if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value, out int left))
            SavedLeft = left;
        _doNotSave = false;


        PreviousPort = Port.Text;

        NotifyIcon.ContextMenuStrip = new ContextMenuStrip();
        NotifyIcon.ContextMenuStrip.Items.Add("Показать окно", null, NotifyIcon_ShowClick);
        NotifyIcon.ContextMenuStrip.Items.Add("Выход", null, NotifyIcon_ExitClick);

        AuthExecuteDelegate = OpenDialog;

        Counter.Text = "";

        _stopUI = true;
        Lock.Checked = false;
        _stopUI = false;
        // Специально провоцируем вызов события для инициализации
        Lock.Checked = true;
        //StartServer();
    }

    private void ResidentForm_Load(object sender, EventArgs e)
    {
        Lock.Checked = true;
        Lock.Focus();
        HideTimer.Interval = 100;
        HideTimer.Enabled = true;
    }

    private void HideTimer_Tick(object sender, EventArgs e)
    {
        HideTimer.Enabled = false;

        HideShow(false);
    }

    private void HideShow(bool show)
    {
        if (show)
        {
            if (!ShowInTaskbar)
            {
                var screen = Screen.GetWorkingArea(this);

                if (SavedTop.HasValue && SavedLeft.HasValue &&
                    SavedTop.Value >= 0 && SavedTop.Value + Height < screen.Height &&
                    SavedLeft.Value >= 0 && SavedLeft.Value + Width < screen.Width)

                {
                    Top = SavedTop.Value;
                    Left = SavedLeft.Value;
                }
                else
                {
                    Left = screen.Width - Width - 10;
                    Top = screen.Height - Height - 100;
                }
                ShowInTaskbar = true;
            }
            Visible = true;
        }
        else
        {
            Visible = false;
        }
    }

    private void ResidentForm_Move(object sender, EventArgs e)
    {
        if (Visible)
        {
            SaveConfigTimer.Interval = 1000;
            SaveConfigTimer.Enabled = true;
        }
    }

    private void SaveConfigTimer_Tick(object sender, EventArgs e)
    {
        if (_doNotSave)
            return;

        SaveConfigTimer.Enabled = false;

        Configuration config =
                ConfigurationManager.OpenExeConfiguration(
                ConfigurationUserLevel.None);

        config.AppSettings.Settings.Remove("Top");
        config.AppSettings.Settings.Remove("Left");

        config.AppSettings.Settings.Add("Top", Top.ToString());
        config.AppSettings.Settings.Add("Left", Left.ToString());

        // Save the configuration file.
        config.Save(ConfigurationSaveMode.Modified);
    }

    private void NotifyIcon_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        HideShow(!Visible);
    }

    /*
     * Метод
     * ResidentForm_FormClosed( object? sender, FormClosingEventArgs e )
     * здесь не использовать, т.к. событие перекрывается и обрабатывается
     * в HiddenContent. См. там.
     */

    private void ResidentForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        HideShow(false);
        e.Cancel = RunServer ? /*просто закрывается окно*/ true : /*Выход в меню TrayIcon*/ false;
    }

    private void NotifyIcon_ExitClick(object? sender, EventArgs e)
    {
        NotifyIcon.Visible = false;
        StopServer();
        if (SaveConfigTimer.Enabled)
        {
            SaveConfigTimer.Enabled = false;
            SaveConfig();
        }

        // При вызове Close дальше будет обработка в HiddenContext, см. там.
        Close();
    }

    private void NotifyIcon_ShowClick(object? sender, EventArgs e)
    {
        HideShow(true);
    }

    private void HideButton_Click(object sender, EventArgs e)
    {
        HideShow(false);
    }

    private void SaveConfig()
    {
        if (_doNotSave)
            return;

        // Get the current configuration file.
        Configuration config =
            ConfigurationManager.OpenExeConfiguration(
            ConfigurationUserLevel.None);

        config.AppSettings.Settings.Remove("port");
        config.AppSettings.Settings.Remove("password");
        config.AppSettings.Settings.Remove("logins");

        config.AppSettings.Settings.Add("port", Port.Text);
        config.AppSettings.Settings.Add("password", Password.Text);

        string login = TestLogin.Text;
        if (!TestLogin.Items.Contains(login))
            TestLogin.Items.Add(login);

        StringBuilder sb = new StringBuilder();
        foreach (var s in TestLogin.Items)
        {
            if (sb.Length > 0)
                sb.Append('/');
            sb.Append(s);
        }
        config.AppSettings.Settings.Add("logins", sb.ToString());

        // Save the configuration file.
        config.Save(ConfigurationSaveMode.Modified);
    }

    private void StartServer()
    {
        if (!int.TryParse(Port.Text, out int port))
            return;

        try
        {
            Listener = new HttpListener();
            // Create a http server and start listening for incoming connections
            Listener?.Prefixes.Add($"http://localhost:{port}/");
            Listener?.Start();
            RunServer = true;

            // Handle requests
            _ = Task.Run(HandleIncomingConnections);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message,
                "Ошибка инициализации сервера аутентификации Яндекс.Диска", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }

    private void StopServer()
    {
        RunServer = false;
        Listener?.Abort();
        Listener?.Close();
        Listener = null;
    }

    private void TestButton_Click(object sender, EventArgs e)
    {
        SaveConfig();
        BrowserAppResult response = new BrowserAppResult();
        Dictionary<string, string> headers = new Dictionary<string, string>()
        {
            { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36"},
            { "sec-ch-ua", "\"Google Chrome\";v=\"119\", \"Chromium\";v=\"119\", \"Not?A_Brand\";v=\"24\""},
        };
        OpenDialog(TestLogin.Text, response, headers);
    }

    private void OpenDialog(string desiredLogin, BrowserAppResult response, Dictionary<string, string> headers)
    {
        bool isYandexCloud = false;
        bool isMailCloud = false;

        foreach (var domain in YandexDomains)
        {
            isYandexCloud |= desiredLogin != null && desiredLogin.Contains(string.Concat("@", domain, ".ru"));
        }
        foreach (var domain in MailDomains)
        {
            isMailCloud |= desiredLogin != null && desiredLogin.Contains(string.Concat("@", domain, ".ru"));
        }

        desiredLogin ??= string.Empty;
        desiredLogin = string.Concat(desiredLogin.Split(Path.GetInvalidFileNameChars())).Trim();
        string profile = string.IsNullOrWhiteSpace(desiredLogin) ? "default" : desiredLogin;

        // Переключение на поток, обрабатывающий UI.
        //System.Threading.SynchronizationContext.Current?.Post( ( _ ) =>
        //{
        //	new AuthForm( desiredLogin, response ).ShowDialog();
        //}, null );
        new AuthForm(headers, desiredLogin, profile, ManualCommit.Checked, response, isYandexCloud, isMailCloud).ShowDialog();

        if (response.Cookies != null)
            AuthenticationOkCounter++;
        else
            AuthenticationFailCounter++;

        Counter.Text = $"Входов успешных / не успешных : {AuthenticationOkCounter} / {AuthenticationFailCounter}";
    }

    public async Task HandleIncomingConnections()
    {
        string passwordToCompre = Password.Text;

        while (RunServer)
        {
            try
            {
                if (Listener == null)
                    break;

                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await Listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                using HttpListenerResponse resp = ctx.Response;

                using StreamReader reader = new StreamReader(req.InputStream);
                var request = JsonConvert.DeserializeObject<BrowserAppRequest>(reader.ReadToEnd());

                var login = request?.Login;
                var password = request?.Password;

                Dictionary<string, string> headers = new();
                if (request?.UserAgent is not null)
                    headers.Add("user-agent", request.UserAgent);
                if (request?.SecChUa is not null)
                    headers.Add("sec-ch-ua", request.SecChUa);

                BrowserAppResult response = new BrowserAppResult();

                if (string.IsNullOrEmpty(login))
                    response.ErrorMessage = "Login is not provided";
                else
                if (string.IsNullOrEmpty(password))
                    response.ErrorMessage = "Password is not provided";
                else
                if (password != passwordToCompre)
                    response.ErrorMessage = "Password is wrong";
                else
                {
                    _showBrowserLocker.Wait();
                    // Окно с браузером нужно открыть в потоке, обрабатывающем UI
                    if (TestButton.InvokeRequired)
                        TestButton.Invoke(AuthExecuteDelegate, login, response, headers);
                    else
                        AuthExecuteDelegate(login, response, headers);
                    _showBrowserLocker.Release();
                }

                string text = response.Serialize();
                byte[] data = Encoding.UTF8.GetBytes(text);
                resp.ContentType = "application/json";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.Length;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data);
                resp.Close();
            }
            catch (ObjectDisposedException)
            {
                // Такое исключение при Listener.Abort(), значит работа закончена
                return;
            }
            catch (HttpListenerException)
            {
                if (!RunServer)
                    return;
            }
        }
    }

    private void Lock_CheckedChanged(object sender, EventArgs e)
    {
        if (_stopUI)
            return;

        if (Lock.Checked)
        {
            Port.Enabled = false;
            Password.Enabled = false;
            Lock.Text = "Заблокировано";
            ToolTip.SetToolTip(Lock, "Для изменения порта или пароля снимите блокировку");
            GeneratePassword.Visible = false;

            SaveConfig();
            if (RunServer)
            {
                StopServer();
            }
            StartServer();
        }
        else
        {
            Port.Enabled = true;
            Password.Enabled = true;
            Lock.Text = "Применить";
            ToolTip.SetToolTip(Lock, "Для применения порта и пароля установите блокировку");
            GeneratePassword.Visible = true;
        }
    }

    private void CopyPortPic_Click(object sender, EventArgs e)
    {
        Clipboard.SetText(Port.Text);
        ToolTip.Show("Порт сохранен в буфер обмена", Port, 1000);
    }

    private void CopyPasswordPic_Click(object sender, EventArgs e)
    {
        Clipboard.SetText(Password.Text);
        ToolTip.Show("Пароль сохранен в буфер обмена", Password, 1000);
    }

    private void GeneratePassword_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        if (Lock.Checked)
            return;
        Password.Text = Guid.NewGuid().ToString();
    }

    private void Port_Validating(object sender, CancelEventArgs e)
    {
        if (!int.TryParse(Port.Text, out int value) || value < 1 || value > ushort.MaxValue)
        {
            e.Cancel = true;
        }
    }
}
