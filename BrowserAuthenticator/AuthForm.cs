using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BrowserAuthenticator;

public partial class AuthForm : Form
{
    #region private static partial Regex LoginRegex();

    [GeneratedRegex("\\\\?\"sk\\\\?\":\\\\?\"(?<sk>.*?)\\\\?\"")]
    private static partial Regex SkRegex();

    [GeneratedRegex("\\\\?\"yandexuid\\\\?\":\\\\?\"(?<uuid>.*?)\\\\?\"")]
    private static partial Regex UuidRegex();

    [GeneratedRegex("\\\\?\"login\\\\?\":\\\\?\"(?<login>.*?)\\\\?\"")]
    private static partial Regex LoginRegex();

    #endregion

    private string? _desiredLogin = null;
    private readonly BrowserAppResult _response;
    private bool _exitting;
    //private CoreWebView2ControllerOptions? _options = null;
    //private CoreWebView2Controller? _controller = null;
    private string _profile;
    private bool _isYandexCloud;
    private bool _isMailCloud;
    private bool _manualCommit;
    private string? _html;
    private List<CoreWebView2Cookie>? _cookieList;
    //private Form? _form;
    private WebView2? _webView2;

    public AuthForm(string desiredLogin, string profile, bool manualCommit,
        BrowserAppResult response, bool isYandexCloud, bool isMailCloud)
    {
        InitializeComponent();

        //_form = null;
        _webView2 = null;
        _html = null;
        _cookieList = null;
        _manualCommit = manualCommit;
        _profile = profile;
        _response = response;
        _exitting = false;
        _isYandexCloud = isYandexCloud;
        _isMailCloud = isMailCloud;
        _desiredLogin = desiredLogin;
        Text = $"������ WebDavMailRuCloud ����������� ���� � ������  \x2022  �������: {_profile}";

        StringBuilder sb = new StringBuilder("������� WebDavMailRuCloud ��������� ���� � ������. " +
            "������� � ������ ��� ����������� ������� ������ � ������� ������ �������, ��� ������� ������ ����������.\r\n");
        if (string.IsNullOrWhiteSpace(desiredLogin))
        {
            sb.Append("������������ �� ������ ����� (������� ������ ��� email) " +
                "��� ����������� ������ � ������� ������ � ���. " +
                "��� ���������� �������������� ���������� ������ ������ � ������� ������, " +
                "����� � ������ ������ �������.");
        }
        else
        {
            Text += $", ������� ������: �{desiredLogin}�";

            sb.Append($"������������ ������ login (������� ������ ��� email): �{desiredLogin}�. ");
            if (_isYandexCloud || _isMailCloud)
            {
                sb.Append(
                    "������ ���������� �������������. " +
                    "��� ���������� ����� � ������ ��� ��������� ������� ������ � ������ ������ �������.");
            }
            else
            {
                sb.Append(
                    "��� ���������� �������������� ���������� ������ ������, " +
                    "����� � ���� ��� ��������� ������� ������ � ������ ������ �������.");
            }
        }

        DescriptionText.Text = sb.ToString();

        /*
         * ���� ������� �� default � ������ ����� ������,
         * ���� �����������, ��� ������ ���� ��� �������������.
         * � ����� ������, ������ ���� �� ���� ������,
         * �������� ������� �� ������ �������� � ���� �������������,
         * ��� �� �����, � ���� �� ��������� ����� �������� ������������� ���,
         * ����������� ���� �� �����.
         */
        //#if !DEBUG
        if (profile != "default" && (isYandexCloud || isMailCloud))
        {
            WindowState = FormWindowState.Normal;
            var screen = Screen.GetWorkingArea(this);
            Top = screen.Height + 100;
            ShowInTaskbar = false;
            ShowWindowDelay.Interval = 3000; // 3 �������
        }
        else
        //#endif
        {

            ShowWindow();
        }

        /*
         * ���� �������� ��� �������������� ������������ 4 ������.
         * ���� �� ��� ����� ���� �� ��� �����������, �������� ������������ ��� �� ��,
         * �� ���������� �������, ����� �� ������ �����.
         */
        NobodyHomeTimer.Interval = 4 * 60_000; // 4 minutes to login
        NobodyHomeTimer.Enabled = true;
    }

    private void DelayTimer_Tick(object sender, EventArgs e)
    {
        ShowWindow();
    }

    private void ShowWindow()
    {
        if (_exitting)
            return;

        ShowWindowDelay.Enabled = false;
        var screen = Screen.GetWorkingArea(this);
        Top = screen.Height / 2 - Height / 2;
        WindowState = FormWindowState.Maximized;
        ShowInTaskbar = true;
    }

    private async Task InitializeAsync()
    {
        var env = await CoreWebView2Environment.CreateAsync(
            userDataFolder: Path.Combine("Cloud accounts !!! KEEP FOLDER SECRET !!!", _profile));

        _webView2 = new WebView2();
        await _webView2.EnsureCoreWebView2Async(env);
        WebViewPanel.Controls.Add(_webView2);
        _webView2.Dock = DockStyle.Fill;
        _webView2.CoreWebView2.FrameNavigationCompleted += WebView_NavigationCompleted;


        ShowWindowDelay.Enabled = true;

        if (_isMailCloud)
            _webView2.CoreWebView2.Navigate("https://cloud.mail.ru/home");
        else
        if (_isYandexCloud)
            _webView2.CoreWebView2.Navigate("https://disk.yandex.ru/client/disk");
        else
            _webView2.CoreWebView2.NavigateToString(Resources.start);
    }

    private void AuthForm_Load(object sender, EventArgs e)
    {
        // ���� �� �����-�� ������� ��������� ������,
        // ��������� 5 ���. ��, � ����� ��������� �� �����-��.
        // �������� ��������� ������� - ����� �� � ��� ������.
        for (int retry = 5; retry > 0; retry--)
        {
            try
            {
                _ = InitializeAsync();
                retry = 0;
            }
            catch (Exception)
            {
            }
        }
    }

    private static string GetNameOnly(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        int pos = value.IndexOf('@');
        if (pos == 0)
            return string.Empty;
        if (pos > 0)
            return value.Substring(0, pos);
        return value;
    }

    private void AuthForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        try
        {
            _webView2?.Dispose();
        }
        catch { }
    }

    private void Quit()
    {
        _exitting = true;
        if (InvokeRequired)
        {
            Invoke((MethodInvoker)delegate
            {
                // Running on the UI thread
                ShowWindowDelay.Enabled = false;
                Close();
            });
        }
        else
        {
            // Running on the UI thread
            ShowWindowDelay.Enabled = false;
            Close();
        }
    }

    private void NobodyHomeTimer_Tick(object sender, EventArgs e)
    {
        Quit();
    }

    private void Deny_Click(object sender, EventArgs e)
    {
        Quit();
    }

    private void GotoMail_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        _webView2?.CoreWebView2.Navigate("https://cloud.mail.ru/home");
    }

    private void GotoYandex_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        _webView2?.CoreWebView2.Navigate("https://disk.yandex.ru/client/disk");
    }

    private void Back_Click(object sender, EventArgs e)
    {
        _webView2?.CoreWebView2.GoBack();
    }

    private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        string url = e.IsSuccess ? _webView2?.CoreWebView2.Source ?? string.Empty : string.Empty;
        _html = null;
        _cookieList = null;

        if (e.IsSuccess &&
            _webView2 is not null &&
            (url.StartsWith("https://disk.yandex.ru/client/disk") || url.StartsWith("https://cloud.mail.ru/home")))
        {
            var htmlEncoded = await _webView2!.CoreWebView2.ExecuteScriptAsync("document.body.outerHTML");
            _html = JsonDocument.Parse(htmlEncoded).RootElement.ToString();

            _cookieList = await _webView2.CoreWebView2.CookieManager.GetCookiesAsync(url);

            if (!_manualCommit)
                Extract();
        }

        UseIt.Enabled = _html is not null;
    }

    private void UseIt_Click(object sender, EventArgs e)
    {
        Extract();
    }

    private void Extract()
    {
        if (_html is null || _cookieList is null)
            return;

        var matchSk = SkRegex().Match(_html);
        var matchUuid = UuidRegex().Match(_html);
        var matchLogin = LoginRegex().Match(_html);

        var sk = matchSk.Success ? matchSk.Groups["sk"].Value : string.Empty;
        var uuid = matchUuid.Success ? matchUuid.Groups["uuid"].Value : string.Empty;
        var login = matchLogin.Success ? matchLogin.Groups["login"].Value : string.Empty;

        // ���� � ����� ����� ������ ����� ���� Ivan � � ������ Ivan@yandex.ru,
        // �� �������� ��� � ������ ����, ������� ���, ������� � @

        if (!string.IsNullOrEmpty(sk) && !string.IsNullOrEmpty(uuid) &&
            !string.IsNullOrEmpty(login) &&
            GetNameOnly(login).Equals(GetNameOnly(_desiredLogin), StringComparison.OrdinalIgnoreCase))
        {
            _response.Cloud = null;
            _response.Login = login;
            _response.Sk = sk;
            _response.Uuid = uuid;
            _response.Cookies = new List<BrowserAppCookieResponse>();


            foreach (var item in _cookieList)
            {
                BrowserAppCookieResponse cookie = new BrowserAppCookieResponse()
                {
                    Name = item.Name,
                    Value = item.Value,
                    Path = item.Path,
                    Domain = item.Domain
                };
                _response.Cookies.Add(cookie);

                _response.Cloud ??=
                        item.Domain.Contains(".yandex.ru", StringComparison.InvariantCultureIgnoreCase) ||
                        item.Domain.Contains(".ya.ru", StringComparison.InvariantCultureIgnoreCase)
                        ? "yandex.ru"
                        : "mail.ru";
            }

            Quit();
        }
    }
}
