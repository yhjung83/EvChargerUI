// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.Transmission
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using System;
using System.IO;
using System.Text;
using JoasUtils;
using Newtonsoft.Json.Linq;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class Transmission
  {
    private string appFilePath = Utils.basefilepath;

    public void TransmissionDataSend(
      string httpURL,
      string stationId,
      string chargerId,
      JSonParser jSonParser)
    {
      WebHelper webHelper = new WebHelper();
      string empty1 = string.Empty;
      string empty2 = string.Empty;
      string str1 = this.appFilePath + "\\requestRepeat.txt";
      int num = 1;
      if (this.countLine(str1).Equals(0))
        return;
      try
      {
        using (StreamReader streamReader = new StreamReader(str1, Encoding.Default))
        {
          string str2;
          while ((str2 = streamReader.ReadLine()) != null)
          {
            JObject jobj = (JObject) null;
            try
            {
              str2 = str2.Trim();
              str2 = str2.Replace("\0", "");
            }
            catch (Exception ex)
            {
            }
            string[] strArray = str2.Split('|');
            empty2 = strArray[1];
            string str3 = Encoding.UTF8.GetString(Encoding.Default.GetBytes("{" + empty2 + "}"));
            JObject jobject;
            if (strArray[0].Equals("STATUS"))
            {
              Uri url = new Uri(string.Format(httpURL + "station/chargers/" + stationId + "/" + chargerId + "?param=" + str3));
              jobject = webHelper.Post("STATUS", url, strArray[1], 1000);
              break;
            }
            if (strArray[0].Equals("START"))
            {
              Uri url = new Uri(string.Format(httpURL + "station/chargingStart/" + stationId + "/" + chargerId + "?param=" + str3));
              jobject = webHelper.Post("START", url, strArray[1], 1000);
              break;
            }
            if (strArray[0].Equals("PROCEED"))
            {
              Uri url = new Uri(string.Format(httpURL + "station/chargingInfo/" + stationId + "/" + chargerId + "?param=" + str3));
              jobject = webHelper.Post("PROCEED", url, strArray[1], 1000);
              break;
            }
            if (strArray[0].Equals("END"))
            {
              Uri url = new Uri(string.Format(httpURL + "station/chargingEnd/" + stationId + "/" + chargerId + "?param=" + str3));
              jobject = webHelper.Post("END", url, strArray[1], 1000);
              break;
            }
            if (strArray[0].Equals("USER"))
            {
              Uri url = new Uri(string.Format(httpURL + "station/user/" + stationId + "/" + chargerId + "?param=" + str3));
              jobject = webHelper.Post("USER", url, strArray[1], 1000);
              break;
            }
            if (strArray[0].Equals("FAULT"))
            {
              Uri url = new Uri(string.Format(httpURL + "station/alarmHistory/" + stationId + "/" + chargerId + "?param=" + str3));
              jobject = webHelper.Post("FAULT", url, strArray[1], 1000);
              break;
            }
            try
            {
              num = Convert.ToInt32(jSonParser.GetJSonData(jobj, "response_receive"));
            }
            catch
            {
              num = 0;
            }
          }
        }
        if (num == 1)
          this.transDataDelete(empty2);
      }
      catch (Exception ex)
      {
      }
    }

    private int countLine(string f)
    {
      try
      {
        int num = 0;
        using (StreamReader streamReader = new StreamReader(f))
        {
          while (true)
          {
            switch (streamReader.ReadLine())
            {
              case null:
                goto label_8;
              default:
                ++num;
                continue;
            }
          }
        }
label_8:
        return num;
      }
      catch (Exception ex)
      {
        return 0;
      }
    }

    private void transDataDelete(string jsonData)
    {
      string str1 = (string) null;
      string str2 = this.appFilePath + "\\requestRepeat.txt";
      try
      {
        str1 = this.appFilePath + "\\temp.txt";
        using (StreamReader streamReader = new StreamReader(str2))
        {
          using (StreamWriter streamWriter = new StreamWriter(str1, false, streamReader.CurrentEncoding))
          {
            string str3;
            while ((str3 = streamReader.ReadLine()) != null)
            {
              string str4 = str3.Replace(" ", "");
              if (!str4.Contains(jsonData))
                streamWriter.WriteLine(str4);
            }
          }
        }
        File.Delete(str2);
        File.Move(str1, str2);
      }
      catch (Exception ex)
      {
      }
      finally
      {
        if (str1 != null && File.Exists(str1))
          File.Delete(str1);
      }
    }
  }
}
