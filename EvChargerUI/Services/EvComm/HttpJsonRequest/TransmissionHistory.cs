// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.TransmissionHistory
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using System;
using System.IO;
using System.Text;
using JoasUtils;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class TransmissionHistory
  {
    public void TransmissionHisotryDataSend(
      string httpURL,
      string stationId,
      string chargerId,
      string date)
    {
      string directoryHistory = this.GetDirectoryHistory(date);
      if (directoryHistory == null)
        return;
      WebHelper webHelper = new WebHelper();
      string empty1 = string.Empty;
      string jsonData = string.Empty;
      string str1 = directoryHistory + "\\ChargingHistory.txt";
      string empty2 = string.Empty;
      if (this.countLine(str1).Equals(0))
        return;
      try
      {
        using (StreamReader streamReader = new StreamReader(str1, Encoding.Default))
        {
          string str2;
          while ((str2 = streamReader.ReadLine()) != null)
          {
            string[] strArray = str2.Split('|');
            jsonData = strArray[1];
            jsonData = this.SetDecryptCardNum(strArray[1]);
            if (strArray[0].Equals("END"))
            {
              Uri url = new Uri(string.Format(httpURL + "station/dChargingEnd/" + stationId + "/" + chargerId));
              empty2 = (string) webHelper.Post("END", url, jsonData, 1000)["response_receive"];
              break;
            }
          }
        }
        if (empty2.Equals("1"))
          this.transDataDelete(jsonData, date);
      }
      catch (Exception ex)
      {
      }
    }

    private string SetDecryptCardNum(string line)
    {
      string str1 = line;
      int num = str1.IndexOf("\"card_num\": \"");
      string str2 = str1.Substring(num + 13, 32);
      Utils utils = new Utils();
      return str1.Replace(str2, utils.DecryptMemberNum(ARIACipher.DEF_ARIA_KEY, str2));
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

    private void transDataDelete(string jsonData, string date)
    {
      string str1 = (string) null;
      string str2 = this.GetDirectoryHistory(date) + "\\ChargingHistory.txt";
      try
      {
        str1 = "@C:\\NANDFlash\\Log\\History\\temp.txt";
        using (StreamReader streamReader = new StreamReader(str2))
        {
          using (StreamWriter streamWriter = new StreamWriter(str1, false, streamReader.CurrentEncoding))
          {
            string str3;
            while ((str3 = streamReader.ReadLine()) != null)
            {
              if (!str3.Contains(jsonData))
                streamWriter.WriteLine(str3);
            }
          }
        }
        File.Delete(str2);
        File.Move(str1, str2);
        this.RemoveDirectory(this.GetDirectoryHistory(date));
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

    private string GetDirectoryHistory(string date)
    {
      string path = "C:\\NANDFlash\\Log\\History\\" + date;
      if (!new DirectoryInfo(path).Exists)
        path = (string) null;
      return path;
    }

    private void RemoveDirectory(string dirPath) => new DirectoryInfo(dirPath).Delete(true);
  }
}
