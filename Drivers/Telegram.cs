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
        public async void SendMessage(string text, string emoji = "", string title = "", int priority = 0)
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
