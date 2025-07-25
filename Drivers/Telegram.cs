using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace vPilot_Pushover.Drivers {
    internal class Telegram : INotifier {

        // Init
        private static readonly HttpClient client = new HttpClient();
        private String settingTelegramBotToken = null;
        private String settingTelegramChatId = null;

        // Event for received commands
        public event Action<string> OnCommandReceived;

        /*
         * 
         * Initilise the driver
         *
        */
        public void init( NotifierConfig config ) {
            this.settingTelegramBotToken = config.settingTelegramBotToken;
            this.settingTelegramChatId = config.settingTelegramChatId;
        }

        /*
         * 
         * Validate the configuration
         *
        */
        public Boolean hasValidConfig() {
            if (this.settingTelegramBotToken == null || this.settingTelegramChatId == null) {
                return false;
            }
            return true;
        }

        /*
         * 
         * Send Pushover message
         *
        */
        public async void sendMessage( String text, String title = "", int priority = 0 ) {

            // Construct the message for Telegram
            string telegramMessage = $"{title}\n\n{text}";
            // Prepare the Telegram API URL
            string telegramApiUrl = $"https://api.telegram.org/bot{settingTelegramBotToken}/sendMessage";

            // Create the form data for the POST request
            var values = new Dictionary<string, string>
            {
                { "chat_id", settingTelegramChatId },
                { "text", telegramMessage }
            };

            // Send the POST request to Telegram
            var response = await client.PostAsync(telegramApiUrl, new FormUrlEncodedContent(values));
            var responseString = await response.Content.ReadAsStringAsync();
        }

        /*
         * 
         * Long polling for Telegram messages
         *
        */
        public async Task StartLongPollingAsync()
        {
            if (!hasValidConfig()) return;

            int offset = 0;
            while (true)
            {
                string url = $"https://api.telegram.org/bot{settingTelegramBotToken}/getUpdates?timeout=30&offset={offset}";
                var response = await client.GetAsync(url);
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
