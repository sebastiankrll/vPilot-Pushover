using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Timers;

namespace vPilot_Pushover
{
    internal class Acars
    {

        // Init
        private static Main s_plugin;
        private static INotifier s_notifier;
        private static List<Dictionary<string, string>> s_hoppieCache = new List<Dictionary<string, string>>();
        private static bool s_cacheLoaded = false;

        private readonly Timer _hoppieTimer = new Timer();

        /*
         * 
         * Initilise the ACARS
         *
        */
        public void Init(Main main, INotifier notifier, string logon)
        {
            s_plugin = main;
            s_notifier = notifier;

            _hoppieTimer.Elapsed += new ElapsedEventHandler(FetchHoppie);
            _hoppieTimer.Interval = 45 * 1000;

        }

        /*
         * 
         * Start the ACARS
         *
        */
        public void Start()
        {
            _hoppieTimer.Enabled = true;
            s_plugin.SendDebug("[ACARS] Starting ACARS");
            FetchHoppie(null, null);
        }

        /*
         * 
         * Stop the ACARS
         *
        */
        public void Stop()
        {
            _hoppieTimer.Enabled = false;
            s_plugin.SendDebug("[ACARS] Stopping ACARS");
        }

        /*
         * 
         * Fetch data from Hoppie API
         *
        */
        private async void FetchHoppie(object source, ElapsedEventArgs e)
        {
            string baseUrl = "http://www.hoppie.nl/acars/system/connect.html";
            string logon = s_plugin._settingHoppieLogon;
            string from = s_plugin.ConnectedCallsign;
            string type = "peek";
            string to = "SERVER";

            if (s_plugin.ConnectedCallsign != null)
            {
                using (HttpClient httpClient = new HttpClient())
                {

                    // Build the complete URL with GET variables
                    string fullUrl = $"{baseUrl}?logon={logon}&from={from}&type={type}&to={to}";
                    s_plugin.SendDebug($"[ACARS] Fetching Hoppie data with callsign {from}");

                    try
                    {
                        HttpResponseMessage response = await httpClient.GetAsync(fullUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            string responseContent = await response.Content.ReadAsStringAsync();
                            ParseHoppie(responseContent);
                        }
                        else
                        {
                            s_plugin.SendDebug($"[ACARS] HttpResponse request failed with status code: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        s_plugin.SendDebug($"[ACARS] An HttpResponse error occurred: {ex.Message}");
                    }

                }
            }
            else
            {
                s_plugin.SendDebug($"[ACARS] fetchHoppie aborted due to missing callsign");
            }

        }

        /*
         * 
         * Parse the Hoppie response
         *
        */
        private void ParseHoppie(string response)
        {

            bool statusOk = response.StartsWith("ok");

            if (statusOk)
            {
                foreach (Match match in Regex.Matches(response, @"\{(\d+)\s(\w+)\s(\w+)\s\{([^\}]+)\}\}"))
                {
                    // Map the Regex groups
                    string key = match.Groups[1].Value;
                    string from = match.Groups[2].Value;
                    string type = match.Groups[3].Value;
                    string message = match.Groups[4].Value;

                    // Clean the messages
                    message = Regex.Replace(match.Groups[4].Value, @"\/data\d\/\d+\/\d*\/.+\/", "");
                    message = Regex.Replace(message, @"@", "");

                    // Check if key doesnt' exist, then add it
                    if (!s_hoppieCache.Exists(x => x["key"] == key))
                    {
                        // Create a dictionary for the current block and add the key-value pairs
                        Dictionary<string, string> dataDict = new Dictionary<string, string>
                        {
                            { "key", key },
                            { "from", from },
                            { "type", type },
                            { "message", message}
                        };

                        // Add the dictionary to the list
                        s_hoppieCache.Add(dataDict);

                        // Send the message to Pushover
                        if (s_cacheLoaded == true && message != "")
                        {
                            s_notifier.SendMessage(message, $"{from} ({type.ToUpper()})");
                        }

                        s_plugin.SendDebug($"[ACARS] Cached {key} with message: {message}");

                    }


                }

                s_cacheLoaded = true;

            }
            else
            {
                s_plugin.SendDebug("[ACARS] okCheck Error: " + response);
            }

        }

    }
}
