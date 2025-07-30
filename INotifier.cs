using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vPilot_Pushover
{

    public class NotifierConfig
    {
        public string SettingPushoverToken { get; set; }
        public string SettingPushoverUser { get; set; }
        public string SettingPushoverDevice { get; set; }
        public string SettingTelegramBotToken { get; set; }
        public string SettingTelegramChatId { get; set; }
        public string SettingGotifyUrl { get; set; }
        public string SettingGotifyToken { get; set; }
    }

    internal interface INotifier
    {
        void Init(NotifierConfig config);
        void SendMessage(string message, string emoji = "", string title = "", int priority = 0);
        bool HasValidConfig();
    }
}
