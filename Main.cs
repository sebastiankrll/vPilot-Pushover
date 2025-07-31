using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using RossCarlson.Vatsim.Vpilot.Plugins;
using RossCarlson.Vatsim.Vpilot.Plugins.Events;
using RossCarlson.Vatsim.Vpilot.Plugins.Exceptions;

namespace vPilot_Pushover
{
    public class Main : IPlugin
    {

        public static string s_version = "1.2.0";

        // Init
        private IBroker _vPilot;
        private INotifier _notifier;
        private Acars _acars;

        public string Name { get; } = "vPilot Pushover";


        public string ConnectedCallsign = null;
        private enum PendingCommand
        {
            None,
            AwaitingConnParams,
            AwaitingChatParams
        }
        private PendingCommand _pendingCommand = PendingCommand.None;
        private string _chatCallsign = null;
        private bool _relayAllRadio = false;

        // Settings
        private bool _settingsLoaded = false;
        private IniFile _settingsFile;

        private string _settingDriver = null;
        private bool _settingPrivateEnabled = false;
        private bool _settingRadioEnabled = false;
        private bool _settingSelcalEnabled = false;
        private bool _settingHoppieEnabled = false;
        private bool _settingDisconnectEnabled = false;
        private bool _settingSendEnabled = false;
        public string _settingHoppieLogon = null;
        private string _settingPushoverToken = null;
        private string _settingPushoverUser = null;
        private string _settingPushoverDevice = null;
        private string _settingTelegramBotToken = null;
        private string _settingTelegramChatId = null;
        private string _settingGotifyUrl = null;
        private string _settingGotifyToken = null;

        /*
         * 
         * Initilise the plugin
         *
        */
        public void Initialize(IBroker broker)
        {
            _vPilot = broker;
            LoadSettings();

            if (_settingsLoaded)
            {

                // Load the correct notifier driver
                if (_settingDriver.ToLower() == "pushover")
                {
                    _notifier = new Drivers.Pushover();

                    NotifierConfig config;
                    config = new NotifierConfig
                    {
                        SettingPushoverToken = _settingPushoverToken,
                        SettingPushoverUser = _settingPushoverUser,
                        SettingPushoverDevice = _settingPushoverDevice
                    };
                    _notifier.Init(config);
                    if (!_notifier.HasValidConfig())
                    {
                        SendDebug("Pushover API key or user key not set. Check your vPilot-Pushover.ini");
                        return;
                    }

                    SendDebug("Driver set to Pushover");
                }
                else if (_settingDriver.ToLower() == "telegram")
                {
                    _notifier = new Drivers.Telegram();

                    NotifierConfig config;
                    config = new NotifierConfig
                    {
                        SettingTelegramBotToken = _settingTelegramBotToken,
                        SettingTelegramChatId = _settingTelegramChatId
                    };
                    _notifier.Init(config);
                    if (!_notifier.HasValidConfig())
                    {
                        SendDebug("Telegram bot token or chat ID not set. Check your vPilot-Pushover.ini");
                        return;
                    }

                    SendDebug("Driver set to Telegram");
                }
                else if (_settingDriver.ToLower() == "gotify")
                {
                    _notifier = new Drivers.Gotify();

                    NotifierConfig config;
                    config = new NotifierConfig
                    {
                        SettingGotifyUrl = _settingGotifyUrl,
                        SettingGotifyToken = _settingGotifyToken
                    };
                    _notifier.Init(config);
                    if (!_notifier.HasValidConfig())
                    {
                        SendDebug("Invalid Gotify server URL or app token. Check your vPilot-Pushover.ini");
                        return;
                    }

                    SendDebug("Driver set to Gotify");
                }
                else
                {
                    SendDebug("Driver not set correctly. Check your vPilot-Pushover.ini");
                    return;
                }

                // Subscribe to events according to settings
                _vPilot.NetworkConnected += OnNetworkConnectedHandler;
                _vPilot.NetworkDisconnected += OnNetworkDisconnectedHandler;
                if (_settingPrivateEnabled) _vPilot.PrivateMessageReceived += OnPrivateMessageReceivedHandler;
                if (_settingRadioEnabled) _vPilot.RadioMessageReceived += OnRadioMessageReceivedHandler;
                if (_settingSelcalEnabled) _vPilot.SelcalAlertReceived += OnSelcalAlertReceivedHandler;

                // Enable ACARS if Hoppie is enabled
                if (_settingHoppieEnabled)
                {
                    _acars = new Acars();
                    _acars.Init(this, _notifier, _settingHoppieLogon);
                }

                // After initializing subscribe to command events
                if (_notifier is Drivers.Telegram telegramNotifier)
                {
                    telegramNotifier.OnCommandReceived += (msg) =>
                    {
                        if (_settingSendEnabled)
                        {
                            Respond(msg);
                        }
                        else
                        {
                            _notifier.SendMessage("Command sending is disabled. Please enable the 'Send' option in your vPilot-Pushover.ini to use this feature.", "⛔");
                        }
                        SendDebug(msg);
                    };

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await telegramNotifier.StartLongPollingAsync();
                        }
                        catch (Exception ex)
                        {
                            SendDebug($"Polling failed: {ex.Message}");
                        }
                    });
                }

                if (_notifier is Drivers.Telegram)
                {
                    _notifier.SendMessage($"Connected! Running version v{s_version}. Type /help to view available commands.", "👋");
                }
                else
                {
                    _notifier.SendMessage($"Connected! Running version v{s_version}.", "👋");
                }
                SendDebug($"vPilot Pushover connected and enabled on v{s_version}");

                UpdateChecker();

            }
            else
            {
                SendDebug("vPilot Pushover plugin failed to load. Check your vPilot-Pushover.ini");
            }

        }

        /*
         * 
         * Send debug message to vPilot
         *
        */
        public void SendDebug(string text)
        {
            _vPilot.PostDebugMessage(text);
        }

        /*
         * 
         * Request network connection (Telegram only)
         *
        */
        public void RequestConnection(string callsign, string typeCode, string selcalCode)
        {
            try
            {
                _vPilot.RequestConnect(callsign, typeCode, selcalCode);
                _notifier.SendMessage("Connecting ...", "⏳");
            }
            catch (SimNotReadyException ex)
            {
                _notifier.SendMessage("Cannot connect: Simulator is not ready.", "❌");
                SendDebug("Cannot connect: Simulator is not ready. " + ex.Message);
            }
            catch (AlreadyConnectedException ex)
            {
                _notifier.SendMessage("Cannot connect: Already connected.", "❌");
                SendDebug("Cannot connect: Already connected. " + ex.Message);
            }
            catch (Exception ex)
            {
                _notifier.SendMessage("Cannot connect: Unknown error during connect.", "❌");
                SendDebug("Unknown error during connect: " + ex.Message);
            }
        }

        /*
         * 
         * Request network disconnection (Telegram only)
         *
        */
        public void RequestDisconnection()
        {
            try
            {
                _vPilot.RequestDisconnect();
                _notifier.SendMessage("Disconnecting ...", "⏳");
            }
            catch (NotConnectedException ex)
            {
                _notifier.SendMessage("Cannot disconnect: Not connected to the network.", "❌");
                SendDebug("Cannot disconnect: Not connected to the network. " + ex.Message);
            }
            catch (Exception ex)
            {
                _notifier.SendMessage("Cannot disconnect: Unknown error during connect.", "❌");
                SendDebug("Unknown error during disconnect: " + ex.Message);
            }
        }

        /*
         * 
         * Send chat message (Telegram only)
         *
        */
        public void SendChatMessage(string message)
        {
            try
            {
                if (_chatCallsign.Trim().Equals("radio", StringComparison.OrdinalIgnoreCase))
                {
                    _vPilot.SendRadioMessage(message);
                    _notifier.SendMessage("Broadcasted on radio!", "📻");
                }
                else
                {
                    _vPilot.SendPrivateMessage(_chatCallsign, message);
                    _notifier.SendMessage($"Sent to {_chatCallsign}!", "💬");
                }
            }
            catch (NotConnectedException ex)
            {
                _notifier.SendMessage("Cannot send message: Not connected to the network.", "❌");
                SendDebug("Cannot send private message: Not connected to the network. " + ex.Message);
            }
            catch (ArgumentException ex)
            {
                _notifier.SendMessage("Cannot send message: Invalid callsign or message.", "❌");
                SendDebug("Cannot send private message: Invalid callsign or message. " + ex.Message);
            }
            catch (Exception ex)
            {
                _notifier.SendMessage("Cannot send message: Unknown error.", "❌");
                SendDebug("Error sending private message: " + ex.Message);
            }
        }

        /*
         * 
         * Hook: Network connected
         *
        */
        private void OnNetworkConnectedHandler(object sender, NetworkConnectedEventArgs e)
        {
            ConnectedCallsign = e.Callsign;

            if (_settingHoppieEnabled)
            {
                _acars.Start();
            }

            _notifier.SendMessage($"Connected as {ConnectedCallsign}!", "🟢", "vPilot", 1);
        }
        /*
         * 
         * Hook: Network disconnected
         *
        */
        private void OnNetworkDisconnectedHandler(object sender, EventArgs e)
        {
            ConnectedCallsign = null;

            if (_settingHoppieEnabled)
            {
                _acars.Stop();
            }

            if (_settingDisconnectEnabled)
            {
                _notifier.SendMessage("Disconnected!", "🔴", "vPilot", 1);
            }
        }

        /*
         * 
         * Hook: Private Message
         *
        */
        private void OnPrivateMessageReceivedHandler(object sender, PrivateMessageReceivedEventArgs e)
        {
            string from = e.From;
            string message = e.Message;

            _notifier.SendMessage(message, "✉️", from, 1);
        }

        /*
         * 
         * Hook: Radio Message
         *
        */
        private void OnRadioMessageReceivedHandler(object sender, RadioMessageReceivedEventArgs e)
        {
            string from = e.From;
            string message = e.Message;

            if (message.Contains(ConnectedCallsign))
            {
                _notifier.SendMessage(message, "📻❗", from, 1);
            }
            else if (_relayAllRadio && !message.Contains(ConnectedCallsign))
            {
                _notifier.SendMessage(message, "📻", from, 1);
            }
        }

        /*
         * 
         * Hook: SELCAL Message
         *
        */
        private void OnSelcalAlertReceivedHandler(object sender, SelcalAlertReceivedEventArgs e)
        {
            string from = e.From;

            _notifier.SendMessage("SELCAL alert", "🔔", from, 1);
        }

        /*
         * 
         * Load plugin settings
         *
        */
        private void LoadSettings()
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\vPilot");
            if (registryKey != null)
            {
                string vPilotPath = (string)registryKey.GetValue("Install_Dir");
                string configFile = vPilotPath + "\\Plugins\\vPilot-Pushover.ini";
                _settingsFile = new IniFile(configFile);

                // Set all values
                _settingDriver = _settingsFile.KeyExists("Driver", "General") ? _settingsFile.Read("Driver", "General") : null;
                _settingPushoverToken = _settingsFile.KeyExists("ApiKey", "Pushover") ? _settingsFile.Read("ApiKey", "Pushover") : null;
                _settingPushoverUser = _settingsFile.KeyExists("UserKey", "Pushover") ? _settingsFile.Read("UserKey", "Pushover") : null;
                _settingPushoverDevice = _settingsFile.KeyExists("Device", "Pushover") ? _settingsFile.Read("Device", "Pushover") : null;
                _settingHoppieEnabled = _settingsFile.KeyExists("Enabled", "Hoppie") ? bool.Parse(_settingsFile.Read("Enabled", "Hoppie")) : false;
                _settingHoppieLogon = _settingsFile.KeyExists("LogonCode", "Hoppie") ? _settingsFile.Read("LogonCode", "Hoppie") : null;
                _settingPrivateEnabled = _settingsFile.KeyExists("Enabled", "RelayPrivate") ? bool.Parse(_settingsFile.Read("Enabled", "RelayPrivate")) : false;
                _settingRadioEnabled = _settingsFile.KeyExists("Enabled", "RelayRadio") ? bool.Parse(_settingsFile.Read("Enabled", "RelayRadio")) : false;
                _settingSelcalEnabled = _settingsFile.KeyExists("Enabled", "RelaySelcal") ? bool.Parse(_settingsFile.Read("Enabled", "RelaySelcal")) : false;
                _settingTelegramBotToken = _settingsFile.KeyExists("BotToken", "Telegram") ? _settingsFile.Read("BotToken", "Telegram") : null;
                _settingTelegramChatId = _settingsFile.KeyExists("ChatId", "Telegram") ? _settingsFile.Read("ChatId", "Telegram") : null;
                _settingDisconnectEnabled = _settingsFile.KeyExists("Enabled", "Disconnect") ? bool.Parse(_settingsFile.Read("Enabled", "Disconnect")) : false;
                _settingSendEnabled = _settingsFile.KeyExists("Enabled", "Send") ? bool.Parse(_settingsFile.Read("Enabled", "Send")) : false;
                _settingGotifyUrl = _settingsFile.KeyExists("Url", "Gotify") ? _settingsFile.Read("Url", "Gotify") : null;
                _settingGotifyToken = _settingsFile.KeyExists("Token", "Gotify") ? _settingsFile.Read("Token", "Gotify") : null;

                // Validate values
                if (_settingHoppieEnabled && _settingHoppieLogon == null)
                {
                    SendDebug("Hoppie logon code not set. Check your vPilot-Pushover.ini");
                    _notifier.SendMessage("Hoppie logon code not set. Check your vPilot-Pushover.ini", "⚠️");
                }

                _settingsLoaded = true;

            }
            else
            {
                SendDebug("Registry key not found. Is vPilot installed correctly?");
            }

        }

        /*
         * 
         * Check if is update available
         *
        */
        private async void UpdateChecker()
        {

            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync("https://raw.githubusercontent.com/blt950/vPilot-Pushover/main/version.txt");
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        if (responseContent != (s_version))
                        {
                            await Task.Delay(5000);
                            SendDebug($"Update available. Latest version is v{responseContent}");
                            _notifier.SendMessage($"Update available. Latest version is v{responseContent}. Download newest version at https://blt950.com", "🚀");
                        }
                    }
                    else
                    {
                        SendDebug($"[Update Checker] HttpResponse request failed with status code: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    SendDebug($"[Update Checker] An HttpResponse error occurred: {ex.Message}");
                }
            }

        }

        /*
         * 
         * Response handler  (Telegram only)
         *
        */
        public void Respond(string msg)
        {
            // Cancel all pending commands
            if (msg.StartsWith("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (_pendingCommand == PendingCommand.None && !string.IsNullOrEmpty(_chatCallsign))
                {
                    _chatCallsign = null;
                    _notifier.SendMessage("Chat mode exited.", "💬");
                }
                else if (_pendingCommand != PendingCommand.None)
                {
                    _notifier.SendMessage("Pending command canceled.", "❌");
                    _pendingCommand = PendingCommand.None;
                }
                else
                {
                    _notifier.SendMessage("Nothing to cancel.", "❌");
                    _pendingCommand = PendingCommand.None;
                }
                return;
            }

            // Handle follow-up for multi-step commands
            if (_pendingCommand == PendingCommand.AwaitingConnParams)
            {
                var parts = msg.Split(':');
                if (parts.Length >= 2)
                {
                    string callsign = parts[0];
                    string typeCode = parts[1];
                    string selcalCode = parts.Length >= 3 ? parts[2] : "";
                    RequestConnection(callsign, typeCode, selcalCode);
                    _pendingCommand = PendingCommand.None;
                }
                else
                {
                    _notifier.SendMessage("Invalid format. Enter: <callsign>:<typecode>:[<selcalcode>]", "⚠️");
                }
                return;
            }
            else if (_pendingCommand == PendingCommand.AwaitingChatParams)
            {
                string callsign = msg.Trim();
                if (!string.IsNullOrEmpty(callsign) && callsign.Trim().Equals("radio", StringComparison.OrdinalIgnoreCase))
                {
                    _notifier.SendMessage($"Now chatting on radio frequency ...", "💬");
                    _chatCallsign = "radio";
                    _pendingCommand = PendingCommand.None;
                }
                else if (!string.IsNullOrEmpty(callsign) && callsign.Length >= 3)
                {
                    _notifier.SendMessage($"Now chatting with {callsign} ...", "💬");
                    _chatCallsign = callsign;
                    _pendingCommand = PendingCommand.None;
                }
                else
                {
                    _notifier.SendMessage("Invalid format. Enter: <callsign>", "⚠️");
                }
                return;
            }

            // Handle initial commands
            if (msg.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
            {
                _notifier.SendMessage(
                    string.Join("\n", new[]
                    {
                        "Need help? Here are the commands you can use:",
                        "",
                        "/conn - Connect to network",
                        "/disc - Disconnect from network",
                        "/chat - Open a chat",
                        "/cancel - Cancel or close chat",
                        "/radio - Toggle radio listening",
                        "/help - Show available commands",
                        "",
                        "Need help setting up Telegram bot commands? Check the full setup guide and command examples here:",
                        "https://github.com/sebastiankrll/vPilot-Pushover/blob/main/telegram.md"
                    }),
                    "🆘"
                );
            }
            else if (msg.StartsWith("/conn", StringComparison.OrdinalIgnoreCase))
            {
                _notifier.SendMessage("Enter: <callsign>:<typecode>:[<selcalcode>]", "✏️");
                _pendingCommand = PendingCommand.AwaitingConnParams;
            }
            else if (msg.StartsWith("/disc", StringComparison.OrdinalIgnoreCase))
            {
                RequestDisconnection();
            }
            else if (msg.StartsWith("/chat", StringComparison.OrdinalIgnoreCase))
            {
                _notifier.SendMessage("Enter: <callsign> or 'radio'", "✏️");
                _pendingCommand = PendingCommand.AwaitingChatParams;
            }
            else if (msg.StartsWith("/radio", StringComparison.OrdinalIgnoreCase))
            {
                _relayAllRadio = !_relayAllRadio;
                if (_relayAllRadio)
                {
                    _notifier.SendMessage("Radio listening enabled!", "🟢");
                }
                else
                {
                    _notifier.SendMessage("Radio listening disabled!", "🔴");
                }
            }
            else if (!msg.StartsWith("/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_chatCallsign))
            {
                SendChatMessage(msg);
            }
            else if (msg.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                _notifier.SendMessage("Unknown command.", "⛔");
            }
            else
            {
                _notifier.SendMessage("No chat open.", "⛔");
            }
        }

    }
}