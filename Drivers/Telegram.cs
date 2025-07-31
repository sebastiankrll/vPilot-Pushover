using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace vPilot_Pushover.Drivers
{
    internal class Telegram : INotifier
    {

        // Init
        private static readonly HttpClient s_client = new HttpClient();
        private string _settingTelegramBotToken = null;
        private string _settingTelegramChatId = null;
        private int _lastPinnedMessageId = 0;

        // Event for received commands
        public event Action<string> OnCommandReceived;

        /*
         * 
         * Initilise the driver
         *
        */
        public void Init(NotifierConfig config)
        {
            _settingTelegramBotToken = config.SettingTelegramBotToken;
            _settingTelegramChatId = config.SettingTelegramChatId;
        }

        /*
         * 
         * Validate the configuration
         *
        */
        public bool HasValidConfig()
        {
            if (_settingTelegramBotToken == null || _settingTelegramChatId == null)
            {
                return false;
            }
            return true;
        }

        /*
         * 
         * Send Pushover message
         *
        */
        public async void SendMessage(string text, string emoji = "", string title = "", int priority = 0, PinMode pinMode = PinMode.None)
        {
            // Construct the message for Telegram
            string emojiPart = string.IsNullOrEmpty(emoji) ? "" : $"{emoji}  ";
            string titlePart = string.IsNullOrEmpty(title) ? "" : $"({title}): ";
            string telegramMessage = $"{emojiPart}{titlePart}{text}";

            // Prepare the Telegram API URL
            string telegramApiUrl = $"https://api.telegram.org/bot{_settingTelegramBotToken}/sendMessage";

            // Create the form data for the POST request
            var values = new Dictionary<string, string>
            {
                { "chat_id", _settingTelegramChatId },
                { "text", telegramMessage }
            };

            // Send the POST request to Telegram
            var response = await s_client.PostAsync(telegramApiUrl, new FormUrlEncodedContent(values));
            var responseString = await response.Content.ReadAsStringAsync();

            // Parse the message_id from the response
            int? messageId = null;
            try
            {
                var json = JObject.Parse(responseString);
                messageId = (int?)json["result"]?["message_id"];
            }
            catch
            {
                // Ignore parse errors
            }

            // Pin or unpin if requested
            if (pinMode == PinMode.Pin && messageId.HasValue)
            {
                // Unpin the last pinned message if there is one
                if (_lastPinnedMessageId != 0)
                {
                    string unpinUrl = $"https://api.telegram.org/bot{_settingTelegramBotToken}/unpinChatMessage";
                    var unpinValues = new Dictionary<string, string>
                    {
                        { "chat_id", _settingTelegramChatId },
                        { "message_id", _lastPinnedMessageId.ToString() }
                    };
                    await s_client.PostAsync(unpinUrl, new FormUrlEncodedContent(unpinValues));
                }

                // Pin the new message
                string pinUrl = $"https://api.telegram.org/bot{_settingTelegramBotToken}/pinChatMessage";
                var pinValues = new Dictionary<string, string>
                {
                    { "chat_id", _settingTelegramChatId },
                    { "message_id", messageId.Value.ToString() }
                };
                await s_client.PostAsync(pinUrl, new FormUrlEncodedContent(pinValues));
                _lastPinnedMessageId = messageId.Value;
            }
            else if (pinMode == PinMode.Unpin && _lastPinnedMessageId != 0)
            {
                string unpinUrl = $"https://api.telegram.org/bot{_settingTelegramBotToken}/unpinChatMessage";
                var unpinValues = new Dictionary<string, string>
                {
                    { "chat_id", _settingTelegramChatId },
                    { "message_id", _lastPinnedMessageId.ToString() }
                };
                await s_client.PostAsync(unpinUrl, new FormUrlEncodedContent(unpinValues));
                _lastPinnedMessageId = 0;
            }
        }

        /*
         * 
         * Long polling for Telegram messages
         *
        */
        public async Task StartLongPollingAsync()
        {
            if (!HasValidConfig()) return;

            int offset = 0;
            while (true)
            {
                string url = $"https://api.telegram.org/bot{_settingTelegramBotToken}/getUpdates?timeout=30&offset={offset}";
                var response = await s_client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                var updates = JObject.Parse(json)["result"];
                foreach (var update in updates)
                {
                    offset = (int)update["update_id"] + 1;
                    var message = update["message"];
                    if (message != null && message["text"] != null)
                    {
                        string text = message["text"].ToString();
                        OnCommandReceived?.Invoke(text);
                    }
                }

                // Throttle the requests to avoid hitting the API limits
                await Task.Delay(1000);
            }
        }
    }
}
