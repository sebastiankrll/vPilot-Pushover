using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace vPilot_Pushover.Drivers
{
    internal class Pushover : INotifier
    {

        // Init
        private static readonly HttpClient s_client = new HttpClient();
        private string _settingPushoverToken = null;
        private string _settingPushoverUser = null;
        private string _settingPushoverDevice = null;

        /*
         * 
         * Initilise the driver
         *
        */
        public void Init(NotifierConfig config)
        {
            _settingPushoverToken = config.SettingPushoverToken;
            _settingPushoverUser = config.SettingPushoverUser;
            _settingPushoverDevice = config.SettingPushoverDevice;
        }

        /*
         * 
         * Validate the configuration
         *
        */
        public bool HasValidConfig()
        {
            if (_settingPushoverToken == null || _settingPushoverUser == null)
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
            var values = new Dictionary<string, string>
            {
                { "token", _settingPushoverToken },
                { "user", _settingPushoverUser },
                { "title",  title },
                { "message", text },
                { "priority", priority.ToString() },
                { "device", _settingPushoverDevice != "" ? _settingPushoverDevice : "" }
            };

            var response = await s_client.PostAsync("https://api.pushover.net/1/messages.json", new FormUrlEncodedContent(values));
            var responseString = await response.Content.ReadAsStringAsync();
        }
    }
}