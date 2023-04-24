using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using YandexAuthBrowser;

namespace YandexAuthBrowser
{
    public partial class AuthForm : Form
    {
        private readonly string? DesiredLogin;
        private readonly BrowserAppResponse Response;
        private bool WeAreFinished;

        public AuthForm(string desiredLogin, BrowserAppResponse response)
        {
            WeAreFinished = false;

            InitializeComponent();

            DesiredLogin = desiredLogin;
            Response = response;

            if (!string.IsNullOrEmpty(DesiredLogin))
            {
                this.Text += $"   ��������� ������� ������: {DesiredLogin}";
            }

            /*
             * ������� ���� ������������ �� ������� ������,
             * ��� ��� ��������������, � ��� �������������� �������,
             * � �������� ����������� ��������...
             * � ������ ����� ���������� ���� �� ����:
             * - ���� ������������ ��� ���������������� �� �����,
             *   ����� ����, ������� ��� ������ ����������, �����������;
             *   � � ����� ������ ��� ������������� ������� �� ������ �����,
             *   ����� ������������ �� ��������� ��������� - � ��� ��� ����?
             * - ���� ������������ �� ���������������� �� ����� ��� ���-�� ����� �� ���,
             *   ����� ����� 3 ������� ���� ������������ � ������������� �� ���� �����
             *   ��� ����� ������������ � ������� ������ �����.
             * ��������, 3 ������� �� ��������� ������������������� �������� - ����������.
             */

            WindowState = FormWindowState.Normal;
            var screen = Screen.GetWorkingArea(this);
            Top = screen.Height + 100;
            ShowInTaskbar = false;
            NobodyHomeTimer.Interval = 4 * 60_000; // 4 minutes to login
            NobodyHomeTimer.Enabled = true;
            DelayTimer.Interval = 3000;
            DelayTimer.Enabled = true;
        }
        private void DelayTimer_Tick(object sender, EventArgs e)
        {
            if (WeAreFinished)
                return;

            DelayTimer.Enabled = false;
            var screen = Screen.GetWorkingArea(this);

            Top = screen.Height / 2 - Height / 2;
            WindowState = FormWindowState.Maximized;
            ShowInTaskbar = true;
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

        private async Task InitializeAsync()
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: DesiredLogin ?? "default");

            // if the control is not visible - this will keep waiting
            await WebView.EnsureCoreWebView2Async(env);

            // any code here is not fired until the control becomes visible
            WebView.CoreWebView2.Navigate("https://disk.yandex.ru/client/disk");
        }

        private async void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && WebView.CoreWebView2.Source.StartsWith("https://disk.yandex.ru/client/disk"))
            {
                var htmlEncoded = await WebView.CoreWebView2.ExecuteScriptAsync("document.body.outerHTML");
                var html = JsonDocument.Parse(htmlEncoded).RootElement.ToString();

                var matchSk = Regex.Match(html, @"\\?""sk\\?"":\\?""(?<sk>.*?)\\?""");
                var matchUuid = Regex.Match(html, @"\\?""yandexuid\\?"":\\?""(?<uuid>.*?)\\?""");
                var matchLogin = Regex.Match(html, @"\\?""login\\?"":\\?""(?<login>.*?)\\?""");

                var sk = matchSk.Success ? matchSk.Groups["sk"].Value : string.Empty;
                var uuid = matchUuid.Success ? matchUuid.Groups["uuid"].Value : string.Empty;
                var login = matchLogin.Success ? matchLogin.Groups["login"].Value : string.Empty;

                // ���� � ����� ����� ������ ����� ���� Ivan � � ������ Ivan@yandex.ru,
                // �� �������� ��� � ������ ����, ������� ���, ������� � @

                if (!string.IsNullOrEmpty(sk) && !string.IsNullOrEmpty(uuid) &&
                    !string.IsNullOrEmpty(login) &&
                    GetNameOnly(login).Equals(GetNameOnly(DesiredLogin), StringComparison.OrdinalIgnoreCase))
                {
                    Response.Login = login;
                    Response.Sk = sk;
                    Response.Uuid = uuid;
                    Response.Cookies = new List<BrowserAppCookieResponse>();

                    var list = await WebView.CoreWebView2.CookieManager.GetCookiesAsync("https://disk.yandex.ru/client/disk");
                    foreach (var item in list)
                    {
                        BrowserAppCookieResponse cookie = new BrowserAppCookieResponse()
                        {
                            Name = item.Name,
                            Value = item.Value,
                            Path = item.Path,
                            Domain = item.Domain
                        };
                        Response.Cookies.Add(cookie);
                    }

                    WeAreFinished = true;
                    if (this.InvokeRequired)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            // Running on the UI thread
                            DelayTimer.Enabled = false;
                            this.Close();
                        });
                    }
                    else
                    {
                        // Running on the UI thread
                        DelayTimer.Enabled = false;
                        this.Close();
                    }
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
                WebView?.Dispose();
            }
            catch { }
        }

        private void NobodyHomeTimer_Tick(object sender, EventArgs e)
        {
            WeAreFinished = true;
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    // Running on the UI thread
                    DelayTimer.Enabled = false;
                    this.Close();
                });
            }
            else
            {
                // Running on the UI thread
                DelayTimer.Enabled = false;
                this.Close();
            }
        }
    }
}
