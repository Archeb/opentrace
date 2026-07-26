using System;
using System.Reflection;
using System.Threading.Tasks;
using Eto.Forms;
using Eto.Drawing;
using Newtonsoft.Json.Linq;
using Resources = OpenTrace.Properties.Resources;

namespace OpenTrace.UI.Dialogs
{
    internal class NextTraceApiV4TokenResult
    {
        public string Token { get; set; }
        public string ExpiresAt { get; set; }
    }

    internal class NextTraceApiV4TokenDialog : Dialog<NextTraceApiV4TokenResult>
    {
        internal const string TokenPageUrl = "https://api.nxtrace.org/v4/api-tokens";

        private const string TokenBridgeScript = @"
(function () {
  if (window.__openTraceV4TokenBridge) {
    window.__openTraceV4TokenBridge.sendToken();
    return 'ready';
  }

  var storageKey = 'nexttrace_v4_public_token_v1';
  var bridge = {
    sentToken: '',
    lastIssuedResponse: '',
    sendToken: function () {
      try {
        var record = null;
        var raw = window.localStorage.getItem(storageKey);
        if (raw) record = JSON.parse(raw);
        var output = document.getElementById('token-output');
        var status = document.getElementById('token-status-value');
        var token = (record && record.token) || (output && output.value) || '';
        if (!token || !status || status.textContent.trim() !== '可用' || token === bridge.sentToken) return;
        bridge.sentToken = token;
        window.eto.postMessage(JSON.stringify({
          type: 'nexttrace-api-v4-token',
          token: token,
          expires_at: (record && record.expires_at) || ''
        }));
      } catch (error) {
      }
    },
    tick: function () {
      bridge.sendToken();
      var response = document.querySelector(
        'input[name=""cf-turnstile-response""], textarea[name=""cf-turnstile-response""]'
      );
      var value = response && response.value;
      var issueButton = document.getElementById('issue-button');
      if (value && value !== bridge.lastIssuedResponse && issueButton && !issueButton.disabled) {
        bridge.lastIssuedResponse = value;
        issueButton.click();
      }
    }
  };

  window.__openTraceV4TokenBridge = bridge;
  window.setInterval(bridge.tick, 400);
  bridge.sendToken();

  var existingOutput = document.getElementById('token-output');
  if (!existingOutput || !existingOutput.value) {
    var startButton = document.getElementById('start-button');
    if (startButton) window.setTimeout(function () { startButton.click(); }, 0);
  }
  return 'ready';
}());";

        private const string ClearSiteDataScript = @"
(async function () {
  try { window.localStorage.clear(); } catch (error) {}
  try { window.sessionStorage.clear(); } catch (error) {}
  try {
    if ('caches' in window) {
      var cacheNames = await window.caches.keys();
      await Promise.all(cacheNames.map(function (name) { return window.caches.delete(name); }));
    }
  } catch (error) {}
  try {
    if ('serviceWorker' in navigator) {
      var registrations = await navigator.serviceWorker.getRegistrations();
      await Promise.all(registrations.map(function (registration) { return registration.unregister(); }));
    }
  } catch (error) {}
  try {
    document.cookie.split(';').forEach(function (cookie) {
      var separator = cookie.indexOf('=');
      var name = (separator >= 0 ? cookie.substring(0, separator) : cookie).trim();
      if (!name) return;
      var expired = name + '=; expires=Thu, 01 Jan 1970 00:00:00 GMT; max-age=0; path=/';
      document.cookie = expired;
      document.cookie = expired + '; domain=' + window.location.hostname;
      document.cookie = expired + '; domain=.' + window.location.hostname;
    });
  } catch (error) {}
  return 'cleared';
}());";

        private readonly WebView tokenWebView;
        private readonly Button confirmButton;
        private readonly Button clearBrowserDataButton;
        private readonly Label statusLabel;
        private NextTraceApiV4TokenResult pendingResult;

        public NextTraceApiV4TokenDialog()
        {
            Title = Resources.NEXTTRACE_API_V4_DIALOG_TITLE;
            ClientSize = new Size(760, 680);
            MinimumSize = new Size(560, 480);
            Padding = new Padding(10);

            tokenWebView = new WebView
            {
                BrowserContextMenuEnabled = false
            };
            tokenWebView.DocumentLoading += TokenWebView_DocumentLoading;
            tokenWebView.DocumentLoaded += TokenWebView_DocumentLoaded;
            tokenWebView.MessageReceived += TokenWebView_MessageReceived;

            confirmButton = new Button
            {
                Text = Resources.NEXTTRACE_API_V4_CONFIRM,
                Enabled = false
            };
            confirmButton.Click += ConfirmButton_Click;

            var cancelButton = new Button
            {
                Text = Resources.CANCEL
            };
            cancelButton.Click += (sender, e) => Close(null);

            clearBrowserDataButton = new Button
            {
                Text = Resources.NEXTTRACE_API_V4_CLEAR_BROWSER_DATA
            };
            clearBrowserDataButton.Click += ClearBrowserDataButton_Click;

            statusLabel = new Label
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            var footerLayout = new TableLayout
            {
                Spacing = new Size(8, 8),
                Rows =
                {
                    new TableRow(
                        clearBrowserDataButton,
                        new TableCell(statusLabel, true),
                        confirmButton,
                        cancelButton)
                }
            };

            Content = new TableLayout
            {
                Spacing = new Size(8, 8),
                Rows =
                {
                    new Label
                    {
                        Text = Resources.NEXTTRACE_API_V4_INSTRUCTIONS
                    },
                    new TableRow(new TableCell(tokenWebView, true)) { ScaleHeight = true },
                    new TableRow(new TableCell(footerLayout, true))
                }
            };

            DefaultButton = confirmButton;
            AbortButton = cancelButton;
            Closed += (sender, e) => tokenWebView.Stop();
            tokenWebView.Url = new Uri(TokenPageUrl);
        }

        private void TokenWebView_DocumentLoading(object sender, WebViewLoadingEventArgs e)
        {
            if (!e.IsMainFrame || e.Uri == null)
                return;

            if (!string.Equals(e.Uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(e.Uri.Host, "api.nxtrace.org", StringComparison.OrdinalIgnoreCase) ||
                !e.Uri.AbsolutePath.StartsWith("/v4/api-tokens", StringComparison.Ordinal))
            {
                e.Cancel = true;
            }
        }

        private async void TokenWebView_DocumentLoaded(object sender, WebViewLoadedEventArgs e)
        {
            if (e.Uri == null ||
                !string.Equals(e.Uri.Host, "api.nxtrace.org", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                await tokenWebView.ExecuteScriptAsync(TokenBridgeScript);
            }
            catch
            {
                // The textbox in Preferences remains available for manual entry
                // on platforms whose WebView cannot inject the bridge.
            }
        }

        private void TokenWebView_MessageReceived(object sender, WebViewMessageEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Message))
                return;

            try
            {
                JObject message = JObject.Parse(e.Message);
                if (!string.Equals(
                    (string)message["type"],
                    "nexttrace-api-v4-token",
                    StringComparison.Ordinal))
                {
                    return;
                }

                string token = ((string)message["token"] ?? "").Trim();
                if (token.Length < 20 || token.Length > 8192)
                    return;

                pendingResult = new NextTraceApiV4TokenResult
                {
                    Token = token,
                    ExpiresAt = ((string)message["expires_at"] ?? "").Trim()
                };
                statusLabel.Text = Resources.NEXTTRACE_API_V4_TOKEN_READY;
                confirmButton.Enabled = true;
            }
            catch
            {
                // Ignore messages that are not emitted by the injected bridge.
            }
        }

        private void ConfirmButton_Click(object sender, EventArgs e)
        {
            if (pendingResult == null || string.IsNullOrWhiteSpace(pendingResult.Token))
                return;

            Close(pendingResult);
        }

        private async void ClearBrowserDataButton_Click(object sender, EventArgs e)
        {
            pendingResult = null;
            confirmButton.Enabled = false;
            clearBrowserDataButton.Enabled = false;
            statusLabel.Text = Resources.NEXTTRACE_API_V4_CLEARING_BROWSER_DATA;

            bool cleared = false;
            try
            {
                await tokenWebView.ExecuteScriptAsync(ClearSiteDataScript);
                cleared = true;
            }
            catch
            {
                // Native WebView clearing below can still remove HttpOnly
                // cookies and the HTTP cache when script access is unavailable.
            }

            try
            {
                cleared |= await TryClearNativeBrowsingDataAsync();
            }
            catch
            {
                // Some Eto backends do not expose their native browser object.
            }

            statusLabel.Text = cleared
                ? Resources.NEXTTRACE_API_V4_BROWSER_DATA_CLEARED
                : Resources.NEXTTRACE_API_V4_BROWSER_DATA_CLEAR_FAILED;
            clearBrowserDataButton.Enabled = true;

            try
            {
                tokenWebView.Url = new Uri(
                    TokenPageUrl + "?opentrace_refresh=" +
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            catch
            {
                tokenWebView.Reload();
            }
        }

        private async Task<bool> TryClearNativeBrowsingDataAsync()
        {
            object coreWebView2 = FindCoreWebView2(tokenWebView.ControlObject, 0) ??
                                  FindCoreWebView2(tokenWebView.Handler, 0);
            if (coreWebView2 == null)
                return false;

            object profile = GetPropertyValue(coreWebView2, "Profile");
            MethodInfo clearBrowsingData = profile?.GetType().GetMethod(
                "ClearBrowsingDataAsync",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            if (clearBrowsingData != null)
            {
                Task clearTask = clearBrowsingData.Invoke(profile, null) as Task;
                if (clearTask != null)
                    await clearTask;
                return true;
            }

            object cookieManager = GetPropertyValue(coreWebView2, "CookieManager");
            MethodInfo deleteAllCookies = cookieManager?.GetType().GetMethod(
                "DeleteAllCookies",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            if (deleteAllCookies == null)
                return false;

            deleteAllCookies.Invoke(cookieManager, null);
            return true;
        }

        private static object FindCoreWebView2(object value, int depth)
        {
            if (value == null || depth > 3)
                return null;

            object coreWebView2 = GetPropertyValue(value, "CoreWebView2");
            if (coreWebView2 != null)
                return coreWebView2;

            string[] childProperties = { "Child", "Control", "WebView", "Browser" };
            foreach (string propertyName in childProperties)
            {
                object child = GetPropertyValue(value, propertyName);
                if (child == null || ReferenceEquals(child, value))
                    continue;

                coreWebView2 = FindCoreWebView2(child, depth + 1);
                if (coreWebView2 != null)
                    return coreWebView2;
            }

            return null;
        }

        private static object GetPropertyValue(object value, string propertyName)
        {
            try
            {
                PropertyInfo property = value?.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property?.GetValue(value, null);
            }
            catch
            {
                return null;
            }
        }
    }
}
