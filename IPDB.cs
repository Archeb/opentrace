using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace OpenTrace
{

    class IPDBLoader
    {
        public static MaxMind.Db.Reader DB;


        public static bool Load()
        {
            if (DB != null)
            {
                DB.Dispose();
                DB = null;
            }
            if (UserSettings.localDBPath == "")
            {
                return false;
            }
            try
            {
                MaxMind.Db.Reader reader = new MaxMind.Db.Reader(UserSettings.localDBPath);
                DB = reader;
            }
            catch (Exception e)
            {
                Eto.Forms.MessageBox.Show($"Cannot load MMDB, Message: ${e.Message} \nSource: ${e.Source} \nStackTrace: ${e.StackTrace}", "Exception Occurred");
            }
            return true;
        }

        public static string Render(string tpl, string original, Dictionary<string, object> data)
        {
            if (tpl != "")
            {
                return Templator.Render(tpl, data);
            }
            return original;
        }

        public static TracerouteResult Rewrite(TracerouteResult r)
        {
            if (DB == null)
            {
                return r;
            }
            Dictionary<string, object> row;
            try
            {
                row = DB.Find<Dictionary<string, object>>(IPAddress.Parse(r.IP));
            }
            catch (Exception)
            {
                return r;
            }
            if (row != null)
            {
                r.Geolocation = Render(UserSettings.localDBAddr, r.Geolocation, row);
                r.Organization = Render(UserSettings.localDBOrg, r.Organization, row);
                r.Latitude = Render(UserSettings.localDBLat, r.Latitude, row);
                r.Longitude = Render(UserSettings.localDBLon, r.Longitude, row);
                r.AS = Render(UserSettings.localDBASN, r.AS, row);
                r.Hostname = Render(UserSettings.localDBHostname, r.Hostname, row);
            }
            return r;
        }
    }

    class Templator
    {
        private static string render(string state, string curKey, object key, object value)
        {
            if (value is Dictionary<string, object>)
            {
                state = render(state, string.Concat(curKey, ".", key), (Dictionary<string, object>)value);
            }
            else if (value is List<object>)
            {
                state = render(state, string.Concat(curKey, ".", key), (List<object>)value);
            }
            else
            {
                var rKey = string.Concat("{", curKey, ".", key.ToString(), "}");
                Console.WriteLine(rKey);
                state = state.Replace(rKey, value.ToString());
            }
            return state;
        }

        private static string render(string state, string key, Dictionary<string, object> data)
        {
            foreach (var item in data)
            {
                state = render(state, key, item.Key, item.Value);
            }
            return state;
        }

        private static string render(string state, string key, List<object> data)
        {
            for (int i = 0; i < data.Count; i++)
            {
                state = render(state, key, i, data[i]);
            }
            return state;
        }

        public static string Render(string tpl, Dictionary<string, object> data)
        {
            var result = render(tpl, "", data);
            var pattern = @"{\..*?}";
            return Regex.Replace(result, pattern, "");
        }
    }
}
