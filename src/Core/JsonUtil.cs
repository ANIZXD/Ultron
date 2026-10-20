using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Ultron.Core
{
    public static class JsonUtil
    {
        public static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            serializer.RecursionLimit = 100;
            return serializer;
        }

        public static Dictionary<string, object> AsDictionary(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static object[] AsArray(object value)
        {
            object[] array = value as object[];
            if (array != null)
            {
                return array;
            }

            // JavaScriptSerializer represents JSON arrays as ArrayList, not object[].
            System.Collections.IList list = value as System.Collections.IList;
            if (list != null)
            {
                object[] copy = new object[list.Count];
                list.CopyTo(copy, 0);
                return copy;
            }

            return null;
        }

        public static string GetString(Dictionary<string, object> dictionary, string key)
        {
            if (dictionary == null)
            {
                return null;
            }

            object value;
            if (!dictionary.TryGetValue(key, out value) || value == null)
            {
                return null;
            }

            return value as string;
        }

        public static bool GetBool(Dictionary<string, object> dictionary, string key)
        {
            if (dictionary == null)
            {
                return false;
            }

            object value;
            if (!dictionary.TryGetValue(key, out value) || value == null)
            {
                return false;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return bool.TryParse(Convert.ToString(value), out parsed) && parsed;
        }

        public static double GetDouble(Dictionary<string, object> dictionary, string key, double fallback)
        {
            if (dictionary == null)
            {
                return fallback;
            }

            object value;
            if (!dictionary.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
