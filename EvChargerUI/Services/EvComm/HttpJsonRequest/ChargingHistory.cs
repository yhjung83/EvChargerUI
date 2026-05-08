// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.ChargingHistory
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using System;
using System.IO;
using System.Text;
using JoasUtils;
using Newtonsoft.Json;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class ChargingHistory
  {
    public void makeRequestRepeat(JSonChargingEnd setData, DateTime uiTime)
    {
      string path = this.GetDirectoryHistory(uiTime) + "\\ChargingHistory.txt";
      try
      {
        string str = "END|" + JsonConvert.SerializeObject((object) this.EncryptMemberNum(setData)) + "\r\n";
        if (File.Exists(path))
          File.AppendAllText(path, str.ToString(), Encoding.Default);
        else
          File.WriteAllText(path, str.ToString(), Encoding.Default);
      }
      catch (Exception ex)
      {
      }
    }

    private JSonChargingEnd EncryptMemberNum(JSonChargingEnd setData)
    {
      string str = new Utils().EncryptMemberNum(ARIACipher.DEF_ARIA_KEY, setData.card_num);
      setData.card_num = str;
      return setData;
    }

    private string GetDirectoryHistory(DateTime uiTime)
    {
      EvCommForm evCommForm = new EvCommForm("");
      string path = "C:\\NANDFlash\\Log\\History\\" + (evCommForm.GetKoreaTimeYear(uiTime) + evCommForm.GetKoreaTimeMonth(uiTime) + evCommForm.GetKoreaTimeDay(uiTime));
      DirectoryInfo directoryInfo = new DirectoryInfo(path);
      if (!directoryInfo.Exists)
        directoryInfo.Create();
      return path;
    }
  }
}
