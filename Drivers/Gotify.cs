using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace vPilot_Pushover.Drivers
{
    internal class Gotify : INotifier
    {

        // Init
        private static readonly HttpClient s_client = new HttpClient();
        private string _settingGotifyUrl = null;
        private string _settingGotifyToken = null;

        /*
         * 
         * Initilise the driver
         *
        */
        public void Init(NotifierConfig config)
        {
            _settingGotifyUrl = config.SettingGotifyUrl;
            _settingGotifyToken = config.SettingGotifyToken;
        }

        /*
         * 
         * Validate the configuration
         *
        */
        public bool HasValidConfig()
        {
            if (_settingGotifyUrl == null || _settingGotifyToken == null)
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
            var values = new Dictionary<string, string>
            {
                { "title",  title },
                { "message", text },
                { "priority", priority.ToString() }
            };

            var response = await s_client.PostAsync(_settingGotifyUrl + "/message?token=" + _settingGotifyToken, new FormUrlEncodedContent(values));
            var responseString = await response.Content.ReadAsStringAsync();
        }
    }
}