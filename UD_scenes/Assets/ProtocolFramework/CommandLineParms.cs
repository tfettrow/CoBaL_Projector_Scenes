using UnityEngine;
using System;
using System.Linq;
using System.Collections.Specialized;

/// <summary>
/// This is a static class that handles dealing with the command line, parsing it into a hash map.
/// Any parms that start with a dash or a slash will be considered a key, and the following value (unless it starts with a dash or slash)
/// will be associated with that key. Multiple keys of the same name  will result in a comma-separated value
/// for that key (ex: -foo A -foo B becomes "foo"->"A,B").
/// </summary>
public static class CommandLineParms
{

   /// <summary>
   /// Static constructor. Parses the command line.
   /// </summary>
   static CommandLineParms()
   {
      parms = new NameValueCollection();

      string[] args = System.Environment.GetCommandLineArgs();

      string currentKey = "";
      for (int i = 1; i < args.Length; ++i)  // skip the first item since it will be the program's pathname
      {
         string s = Uri.UnescapeDataString(args[i]);

         if (s.StartsWith("-") || s.StartsWith("/"))
         {
            currentKey = s.Remove(0, 1);
            parms.Add(currentKey, null);
         }
         else
         {
            parms.Add(currentKey, s);
         }
      }
      foreach (string key in parms)
      {
         Debug.LogFormat("{0}=[{1}]", key, parms[key]);
      }

   }
   /// <summary>
   /// Get the string value for the given command line parm; if it doesn't exist, an empty string is returned.
   /// Note that if there are multiple values for this key (ex: -foo 1 -foo 2) then the results are a comma separated string (1,2).
   /// </summary>
   /// <param name="key">The command line parm</param>
   /// <returns>The value of the key</returns>
   public static string String(string key)
   {
      if (parms[key] != null)
         return parms[key];
      return "";
   }

   /// <summary>
   /// Get the string value for the given command line parm; if it doesn't exist, the default val string is returned.
   /// Note that if there are multiple values for this key (ex: -foo 1 -foo 2) then the results are a comma separated string (1,2).
   /// </summary>
   /// <param name="key">The command line parm</param>
   /// <param name="val">Default value if key does not exist</param>
   /// <returns>The value of the key</returns>
   public static string String(string key, string val)
   {
      if (parms[key] != null)
         val = parms[key];
      return val;
   }

   /// <summary>
   /// Get the intiger value for the given command line parm; if it doesn't exist, the default val is returned.
   /// Note that if there are multiple values for this key (ex: -foo 1 -foo 2) then the results are undefined.
   /// </summary>
   /// <param name="key">The command line parm</param>
   /// <param name="val">Default value if key does not exist</param>
   /// <returns>The intiger value of the key</returns>
   public static int Int(string key, int val)
   {
      if (parms[key] != null)
         val = (int)Convert.ToSingle(parms[key]); // sometimes we get passed a 1.0 and need to allow for it.
      return val;
   }


   /// <summary>
   /// Get the float value for the given command line parm; if it doesn't exist, the default val is returned.
   /// Note that if there are multiple values for this key (ex: -foo 1 -foo 2) then the results are undefined.
   /// </summary>
   /// <param name="key">The command line parm</param>
   /// <param name="val">Default value if key does not exist</param>
   /// <returns>The float value of the key</returns>
   public static float Float(string key, float val)
   {
      if (parms[key] != null)
         val = Convert.ToSingle(parms[key]);
      return val;
   }

   /// <summary>
   /// Get the boolean value for the given command line parm; if it doesn't exist, the default val is returned.
   /// This function handles both numbers (0,1,-1) and strings (false, true). 0 is treated as false, anything else is treated as true.
   /// Note that if there are multiple values for this key (ex: -foo 0 -foo 1) then the results are undefined.
   /// </summary>
   /// <param name="key">The command line parm</param>
   /// <param name="val">Default boolean value if key does not exist</param>
   /// <returns>The value of the key</returns>
   public static bool Bool(string key, bool val)
   {
      if (parms[key] != null)
      {
         string s = parms[key].ToLower();
         if (s == "true")
            return true;
         if (s == "false")
            return false;
         val = Convert.ToInt32(s) != 0;
      }
      return val;
   }

   static float _h(string s)
   {
      float v = Convert.ToSingle(s);
      if (v < 1)
         return 0;
      if (v >= 255)
         return 1;
      return v / 255.0f;
   }

   /// <summary>
   /// Get the given command line parm as a Unity Color object. This method handles both discrete numbers (1,2,3)
   /// and HTML-style hex colors (#010203). Both RGB and RGBA variants are supported.
   /// Note that if there are multiple values for this key (ex: -foo 1 -foo 2) then the results are undefined.
   /// </summary>
   /// <param name="key">The command line parm</param>
   /// <param name="val">Default color value if key does not exist</param>
   /// <returns>A Color object</returns>
   public static Color Color(string key, Color val)
   {
      if (parms[key] != null)
      {
         string[] c = parms[key].Split(',');

         if (c.Length >= 4)
            val = new Color(_h(c[0]), _h(c[1]), _h(c[2]), _h(c[3]));
         else if (c.Length == 3)
            val = new Color(_h(c[0]), _h(c[1]), _h(c[2]));
         else
         {
            string s = parms[key];
            if (s.StartsWith("#"))
            {
               s = s.Remove(0, 1);
               if (s.Length>=6)
               {
                  byte r = byte.Parse(s.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                  byte g = byte.Parse(s.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                  byte b = byte.Parse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                  byte a = 255;
                  if (s.Length>=8)
                     a = byte.Parse(s.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                  val = new Color(r, g, b, a);
               }
            }
         }
      }

      return val;
   }

   /// <summary>
   /// Get the given command line parm as a Unity Vector2 object, that is, two discrete numbers (1.0,2.5)
   /// Note that if there are multiple values for this key (ex: -foo 1 -foo 2) then the results are undefined.
   /// </summary>
   /// <param name="key">The command line parm</param>
   /// <param name="val">Default Vector2 value if key does not exist</param>
   /// <returns>A Vector2 object</returns>
   public static Vector2 Vector2(string key, Vector2 val)
   {
      if (parms[key] != null)
      {
         string[] c = parms[key].Split(',');
         if (c.Length>=2)
            val = new Vector2(Convert.ToSingle(c[0]), Convert.ToSingle(c[1]));
      }

      return val;
   }


   /// <summary>
   /// Get the given command line parm as a Unity Vector3 object, that is, three discrete numbers (1.0,2.5,3.9)
   /// Note that if there are multiple values for this key (ex: -foo 1 -foo 2) then the results are undefined.
   /// </summary>
   /// <param name="key">The command line parm</param>
   /// <param name="val">Default Vector3 value if key does not exist</param>
   /// <returns>A Vector3 object</returns>
   public static Vector3 Vector3(string key, Vector3 val)
   {
      if (parms[key] != null)
      {
         string[] c = parms[key].Split(',');
         if (c.Length >= 3)
            val = new Vector3(Convert.ToSingle(c[0]), Convert.ToSingle(c[1]), Convert.ToSingle(c[2]));
      }

      return val;
   }

   /// <summary>
   /// Checks if the given key is present or not. This is useful for switch-like command line parms that don't
   /// have a value since it will result in an empty string or zero value otherwise.
   /// </summary>
   /// <param name="key">The command line parm</param>
   /// <returns>True if the command line parm was present on the command line, false if not</returns>
   public static bool isSet(string key)
   {
      if (parms.Get(key) == null)
         return parms.AllKeys.Contains(key);
      else
         return true;
   }


   private static NameValueCollection parms;

}
