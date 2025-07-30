using Microsoft.Win32;
using RossCarlson.Vatsim.Vpilot.Plugins;
using RossCarlson.Vatsim.Vpilot.Plugins.Events;
using RossCarlson.Vatsim.Vpilot.Plugins.Exceptions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace vPilot_Pushover
{
    public class Main : IPlugin
    {

        public static string version = "1.2.0";

        // Init
        private IBroker vPilot;
        private INotifier notifier;
        private Acars acars;

        public string Name { get; } = "vPilot Pushover";

        // Public variables
        public string connectedCallsign = null;
        private enum PendingCommand
        {
            None,
            AwaitingConnParams,
            AwaitingChatParams
        }
        private PendingCommand pendingCommand = PendingCommand.None;
        private string chatCallsign = null;
        private bool relayAllRadio = false;

        // Settings
        private Boolean settingsLoaded = false;
        private IniFile settingsFile;

        private String settingDriver = null;
        private Boolean settingPrivateEnabled = false;
        private Boolean settingRadioEnabled = false;
        private Boolean settingSelcalEnabled = false;
        private Boolean settingHoppieEnabled = false;
        private Boolean settingDisconnectEnabled = false;
        private Boolean settingSendEnabled = false;
        public String settingHoppieLogon = null;
        private String settingPushoverToken = null;
        private String settingPushoverUser = null;
        private String settingPushoverDevice = null;
        private String settingTelegramBotToken = null;
        private String settingTelegramChatId = null;
        private String settingGotifyUrl = null;
        private String settingGotifyToken = null;

        /*
         * 
         * Initilise the plugin
         *
        */
        public void Initialize(IBroker broker)
        {
            vPilot = broker;
            loadSettings();

            if (settingsLoaded)
            {

                // Load the correct notifier driver
                if (settingDriver.ToLower() == "pushover")
                {
                    notifier = new Drivers.Pushover();

                    NotifierConfig config;
                    config = new NotifierConfig
                    {
                        settingPushoverToken = settingPushoverToken,
                        settingPushoverUser = settingPushoverUser,
                        settingPushoverDevice = settingPushoverDevice
                    };
                    notifier.init(config);
                    if (!notifier.hasValidConfig())
                    {
                        sendDebug("Pushover API key or user key not set. Check your vPilot-Pushover.ini");
                        return;
                    }

                    sendDebug("Driver set to Pushover");
                }
                else if (settingDriver.ToLower() == "telegram")
                {
                    notifier = new Drivers.Telegram();

                    NotifierConfig config;
                    config = new NotifierConfig
                    {
                        settingTelegramBotToken = settingTelegramBotToken,
                        settingTelegramChatId = settingTelegramChatId
                    };
                    notifier.init(config);
                    if (!notifier.hasValidConfig())
                    {
                        sendDebug("Telegram bot token or chat ID not set. Check your vPilot-Pushover.ini");
                        return;
                    }

                    sendDebug("Driver set to Telegram");
                }
                else if (settingDriver.ToLower() == "gotify")
                {
                    notifier = new Drivers.Gotify();

                    NotifierConfig config;
                    config = new NotifierConfig
                    {
                        settingGotifyUrl = settingGotifyUrl,
                        settingGotifyToken = settingGotifyToken
                    };
                    notifier.init(config);
                    if (!notifier.hasValidConfig())
                    {
                        sendDebug("Invalid Gotify server URL or app token. Check your vPilot-Pushover.ini");
                        return;
                    }

                    sendDebug("Driver set to Gotify");
                }
                else
                {
                    sendDebug("Driver not set correctly. Check your vPilot-Pushover.ini");
                    return;
                }

                // Subscribe to events according to settings
                vPilot.NetworkConnected += onNetworkConnectedHandler;
                vPilot.NetworkDisconnected += onNetworkDisconnectedHandler;
                if (settingPrivateEnabled) vPilot.PrivateMessageReceived += onPrivateMessageReceivedHandler;
                if (settingRadioEnabled) vPilot.RadioMessageReceived += onRadioMessageReceivedHandler;
                if (settingSelcalEnabled) vPilot.SelcalAlertReceived += onSelcalAlertReceivedHandler;

                // Enable ACARS if Hoppie is enabled
                if (settingHoppieEnabled)
                {
                    acars = new Acars();
                    acars.init(this, notifier, settingHoppieLogon);
                }

                // After initializing subscribe to command events
                if (notifier is Drivers.Telegram telegramNotifier)
                {
                    telegramNotifier.OnCommandReceived += (msg) =>
                    {
                        if (settingSendEnabled)
                        {
                            respond(msg);
                        }
                        else
                        {
                            notifier.sendMessage("Command sending is disabled. Please enable the 'Send' option in your vPilot-Pushover.ini to use this feature.", "⛔");
                        }
                        sendDebug(msg);
                    };

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await telegramNotifier.StartLongPollingAsync();
                        }
                        catch (Exception ex)
                        {
                            sendDebug($"Polling failed: {ex.Message}");
                        }
                    });
                }

                if (notifier is Drivers.Telegram)
                {
                    notifier.sendMessage($"Connected! Running version v{version}. Type /help to view available commands.", "👋");
                }
                else
                {
                    notifier.sendMessage($"Connected! Running version v{version}.", "👋");
                }
                sendDebug($"vPilot Pushover connected and enabled on v{version}");

                updateChecker();

            }
            else
            {
                sendDebug("vPilot Pushover plugin failed to load. Check your vPilot-Pushover.ini");
            }

        }

        /*
         * 
         * Send debug message to vPilot
         *
        */
        public void sendDebug(String text)
        {
            vPilot.PostDebugMessage(text);
        }

        /*
         * 
         * Request network connection (Telegram only)
         *
        */
        public void requestConnection(String callsign, String typeCode, String selcalCode)
        {
            try
            {
                vPilot.RequestConnect(callsign, typeCode, selcalCode);
                notifier.sendMessage("Connecting ...", "⏳");
            }
            catch (SimNotReadyException ex)
            {
                notifier.sendMessage("Cannot connect: Simulator is not ready.", "❌");
                sendDebug("Cannot connect: Simulator is not ready. " + ex.Message);
            }
            catch (AlreadyConnectedException ex)
            {
                notifier.sendMessage("Cannot connect: Already connected.", "❌");
                sendDebug("Cannot connect: Already connected. " + ex.Message);
            }
            catch (Exception ex)
            {
                notifier.sendMessage("Cannot connect: Unknown error during connect.", "❌");
                sendDebug("Unknown error during connect: " + ex.Message);
            }
        }

        /*
         * 
         * Request network disconnection (Telegram only)
         *
        */
        public void requestDisconnection()
        {
            try
            {
                vPilot.RequestDisconnect();
                notifier.sendMessage("Disconnecting ...", "⏳");
            }
            catch (NotConnectedException ex)
            {
                notifier.sendMessage("Cannot disconnect: Not connected to the network.", "❌");
                sendDebug("Cannot disconnect: Not connected to the network. " + ex.Message);
            }
            catch (Exception ex)
            {
                notifier.sendMessage("Cannot disconnect: Unknown error during connect.", "❌");
                sendDebug("Unknown error during disconnect: " + ex.Message);
            }
        }

        /*
         * 
         * Send chat message (Telegram only)
         *
        */
        public void sendChatMessage(String message)
        {
            try
            {
                if (chatCallsign.Trim().Equals("radio", StringComparison.OrdinalIgnoreCase))
                {
                    vPilot.SendRadioMessage(message);
                    notifier.sendMessage("Broadcasted on radio!", "📻");
                }
                else
                {
                    vPilot.SendPrivateMessage(chatCallsign, message);
                    notifier.sendMessage($"Sent to {chatCallsign}!", "💬");
                }
            }
            catch (NotConnectedException ex)
            {
                notifier.sendMessage("Cannot send message: Not connected to the network.", "❌");
                sendDebug("Cannot send private message: Not connected to the network. " + ex.Message);
            }
            catch (ArgumentException ex)
            {
                notifier.sendMessage("Cannot send message: Invalid callsign or message.", "❌");
                sendDebug("Cannot send private message: Invalid callsign or message. " + ex.Message);
            }
            catch (Exception ex)
            {
                notifier.sendMessage("Cannot send message: Unknown error.", "❌");
                sendDebug("Error sending private message: " + ex.Message);
            }
        }

        /*
         * 
         * Hook: Network connected
         *
        */
        private void onNetworkConnectedHandler(object sender, NetworkConnectedEventArgs e)
        {
            connectedCallsign = e.Callsign;

            if (settingHoppieEnabled)
            {
                acars.start();
            }

            notifier.sendMessage($"Connected as {connectedCallsign}!", "🟢", "vPilot", 1);
        }
        /*
         * 
         * Hook: Network disconnected
         *
        */
        private void onNetworkDisconnectedHandler(object sender, EventArgs e)
        {
            connectedCallsign = null;

            if (settingHoppieEnabled)
            {
                acars.stop();
            }

            if (settingDisconnectEnabled)
            {
                notifier.sendMessage("Disconnected!", "🔴", "vPilot", 1);
            }
        }

        /*
         * 
         * Hook: Private Message
         *
        */
        private void onPrivateMessageReceivedHandler(object sender, PrivateMessageReceivedEventArgs e)
        {
            string from = e.From;
            string message = e.Message;

            notifier.sendMessage(message, "✉️", from, 1);
        }

        /*
         * 
         * Hook: Radio Message
         *
        */
        private void onRadioMessageReceivedHandler(object sender, RadioMessageReceivedEventArgs e)
        {
            string from = e.From;
            string message = e.Message;

            if (message.Contains(connectedCallsign))
            {
                notifier.sendMessage(message, "📻❗", from, 1);
            }
            else if (relayAllRadio && !message.Contains(connectedCallsign))
            {
                notifier.sendMessage(message, "📻", from, 1);
            }
        }

        /*
         * 
         * Hook: SELCAL Message
         *
        */
        private void onSelcalAlertReceivedHandler(object sender, SelcalAlertReceivedEventArgs e)
        {
            string from = e.From;

            notifier.sendMessage("SELCAL alert", "🔔", from, 1);
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
                settingsFile = new IniFile(configFile);

                // Set all values
                settingDriver = settingsFile.KeyExists("Driver", "General") ? settingsFile.Read("Driver", "General") : null;
                settingPushoverToken = settingsFile.KeyExists("ApiKey", "Pushover") ? settingsFile.Read("ApiKey", "Pushover") : null;
                settingPushoverUser = settingsFile.KeyExists("UserKey", "Pushover") ? settingsFile.Read("UserKey", "Pushover") : null;
                settingPushoverDevice = settingsFile.KeyExists("Device", "Pushover") ? settingsFile.Read("Device", "Pushover") : null;
                settingHoppieEnabled = settingsFile.KeyExists("Enabled", "Hoppie") ? Boolean.Parse(settingsFile.Read("Enabled", "Hoppie")) : false;
                settingHoppieLogon = settingsFile.KeyExists("LogonCode", "Hoppie") ? settingsFile.Read("LogonCode", "Hoppie") : null;
                settingPrivateEnabled = settingsFile.KeyExists("Enabled", "RelayPrivate") ? Boolean.Parse(settingsFile.Read("Enabled", "RelayPrivate")) : false;
                settingRadioEnabled = settingsFile.KeyExists("Enabled", "RelayRadio") ? Boolean.Parse(settingsFile.Read("Enabled", "RelayRadio")) : false;
                settingSelcalEnabled = settingsFile.KeyExists("Enabled", "RelaySelcal") ? Boolean.Parse(settingsFile.Read("Enabled", "RelaySelcal")) : false;
                settingTelegramBotToken = settingsFile.KeyExists("BotToken", "Telegram") ? settingsFile.Read("BotToken", "Telegram") : null;
                settingTelegramChatId = settingsFile.KeyExists("ChatId", "Telegram") ? settingsFile.Read("ChatId", "Telegram") : null;
                settingDisconnectEnabled = settingsFile.KeyExists("Enabled", "Disconnect") ? Boolean.Parse(settingsFile.Read("Enabled", "Disconnect")) : false;
                settingSendEnabled = settingsFile.KeyExists("Enabled", "Send") ? Boolean.Parse(settingsFile.Read("Enabled", "Send")) : false;
                settingGotifyUrl = settingsFile.KeyExists("Url", "Gotify") ? settingsFile.Read("Url", "Gotify") : null;
                settingGotifyToken = settingsFile.KeyExists("Token", "Gotify") ? settingsFile.Read("Token", "Gotify") : null;

                // Validate values
                if (settingHoppieEnabled && settingHoppieLogon == null)
                {
                    sendDebug("Hoppie logon code not set. Check your vPilot-Pushover.ini");
                    notifier.sendMessage("Hoppie logon code not set. Check your vPilot-Pushover.ini", "⚠️");
                }

                settingsLoaded = true;

            }
            else
            {
                sendDebug("Registry key not found. Is vPilot installed correctly?");
            }

        }

        /*
         * 
         * Check if is update available
         *
        */
        private async void updateChecker()
        {

            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync("https://raw.githubusercontent.com/blt950/vPilot-Pushover/main/version.txt");
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        if (responseContent != (version))
                        {
                            await Task.Delay(5000);
                            sendDebug($"Update available. Latest version is v{responseContent}");
                            notifier.sendMessage($"Update available. Latest version is v{responseContent}. Download newest version at https://blt950.com", "🚀");
                        }
                    }
                    else
                    {
                        sendDebug($"[Update Checker] HttpResponse request failed with status code: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    sendDebug($"[Update Checker] An HttpResponse error occurred: {ex.Message}");
                }
            }

        }

        /*
         * 
         * Response handler  (Telegram only)
         *
        */
        public void respond(String msg)
        {
            // Cancel all pending commands
            if (msg.StartsWith("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (pendingCommand == PendingCommand.None && !string.IsNullOrEmpty(chatCallsign))
                {
                    chatCallsign = null;
                    notifier.sendMessage("Chat mode exited.", "💬");
                }
                else
                {
                    notifier.sendMessage("Command canceled!", "❌");
                    pendingCommand = PendingCommand.None;
                }
                return;
            }

            // Handle follow-up for multi-step commands
            if (pendingCommand == PendingCommand.AwaitingConnParams)
            {
                var parts = msg.Split(':');
                if (parts.Length >= 2)
                {
                    string callsign = parts[0];
                    string typeCode = parts[1];
                    string selcalCode = parts.Length >= 3 ? parts[2] : "";
                    requestConnection(callsign, typeCode, selcalCode);
                    pendingCommand = PendingCommand.None;
                }
                else
                {
                    notifier.sendMessage("Invalid format. Enter: <callsign>:<typecode>:[<selcalcode>]", "⚠️");
                }
                return;
            }
            else if (pendingCommand == PendingCommand.AwaitingChatParams)
            {
                string callsign = msg.Trim();
                if (!string.IsNullOrEmpty(callsign))
                {
                    notifier.sendMessage($"Now chatting with {callsign} ...", "💬");
                    chatCallsign = callsign;
                    pendingCommand = PendingCommand.None;
                }
                else
                {
                    notifier.sendMessage("Invalid format. Enter: <callsign>", "⚠️");
                }
                return;
            }

            // Handle initial commands
            if (msg.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
            {
                notifier.sendMessage(
                    @"Need help? Here are the commands you can use:
                    /conn - Connect to network
                    /disc - Disconnect from network
                    /chat - Open a chat
                    /cancel - Cancel command or close chat
                    /radio - Toggle radio listening
                    /help - Show available commands
                    
                    Need help setting up Telegram bot commands? Check the full setup guide and command examples here:
                    https://github.com/sebastiankrll/vPilot-Pushover/blob/main/telegram.md", "🆘"
                );
            }
            else if (msg.StartsWith("/conn", StringComparison.OrdinalIgnoreCase))
            {
                notifier.sendMessage("Enter: <callsign>:<typecode>:[<selcalcode>]", "✏️");
                pendingCommand = PendingCommand.AwaitingConnParams;
            }
            else if (msg.StartsWith("/disc", StringComparison.OrdinalIgnoreCase))
            {
                requestDisconnection();
            }
            else if (msg.StartsWith("/chat", StringComparison.OrdinalIgnoreCase))
            {
                notifier.sendMessage("Enter: <callsign> or 'radio'", "✏️");
                pendingCommand = PendingCommand.AwaitingChatParams;
            }
            else if (msg.StartsWith("/radio", StringComparison.OrdinalIgnoreCase))
            {
                relayAllRadio = !relayAllRadio;
                if (relayAllRadio)
                {
                    notifier.sendMessage("Radio listening enabled!", "🟢");
                }
                else
                {
                    notifier.sendMessage("Radio listening disabled!", "🔴");
                }
            }
            else if (!msg.StartsWith("/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(chatCallsign))
            {
                sendChatMessage(msg);
            }
            else if (msg.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                notifier.sendMessage("Unknown command.", "⛔");
            }
            else
            {
                notifier.sendMessage("No chat open.", "⛔");
            }
        }

    }
}