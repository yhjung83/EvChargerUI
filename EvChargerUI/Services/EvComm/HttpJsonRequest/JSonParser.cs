// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.JSonParser
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using Newtonsoft.Json.Linq;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
    public class JSonParser
    {
        //  public string GetJSonData(JObject jobj, string key)
        //  {
        //    try
        //    {
        //      return jobj?[key].ToString();
        //    }
        //    catch
        //    {
        //      return (string) null;
        //    }
        //  }
        //}
        public string GetJSonData(JObject jobj, string key)
        {
            if (jobj == null)
                return null;

            if (!jobj.ContainsKey(key))
                return null;

            var token = jobj[key];
            return token?.ToString();
        }
    }
}
