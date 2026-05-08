// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.RequestRepeat
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
  public class RequestRepeat
  {
    private string appFilePath = Utils.basefilepath + "\\requestRepeat.txt";

    public void makeRequestRepeat(JSonChargers setData)
    {
      try
      {
        string str = "STATUS|" + JsonConvert.SerializeObject((object) setData) + "\r\n";
        if (File.Exists(this.appFilePath))
          File.AppendAllText(this.appFilePath, str.ToString(), Encoding.Default);
        else
          File.WriteAllText(this.appFilePath, str.ToString(), Encoding.Default);
      }
      catch (Exception ex)
      {
      }
    }

    public void makeRequestRepeat(JSonChargingStart setData)
    {
      try
      {
        string str = "START|" + JsonConvert.SerializeObject((object) setData) + "\r\n";
        if (File.Exists(this.appFilePath))
          File.AppendAllText(this.appFilePath, str.ToString(), Encoding.Default);
        else
          File.WriteAllText(this.appFilePath, str.ToString(), Encoding.Default);
      }
      catch (Exception ex)
      {
      }
    }

    public void makeRequestRepeat(JSonChargingInfo setData)
    {
      try
      {
        string str = "PROCEED|" + JsonConvert.SerializeObject((object) setData) + "\r\n";
        if (File.Exists(this.appFilePath))
          File.AppendAllText(this.appFilePath, str.ToString(), Encoding.Default);
        else
          File.WriteAllText(this.appFilePath, str.ToString(), Encoding.Default);
      }
      catch (Exception ex)
      {
      }
    }

    public void makeRequestRepeat(JSonChargingEnd setData)
    {
      try
      {
        string str = "END|" + JsonConvert.SerializeObject((object) setData) + "\r\n";
        if (File.Exists(this.appFilePath))
          File.AppendAllText(this.appFilePath, str.ToString(), Encoding.Default);
        else
          File.WriteAllText(this.appFilePath, str.ToString(), Encoding.Default);
      }
      catch (Exception ex)
      {
      }
    }

    public void makeRequestRepeat(JSonUser setData)
    {
      try
      {
        string str = "USER|" + JsonConvert.SerializeObject((object) setData) + "\r\n";
        if (File.Exists(this.appFilePath))
          File.AppendAllText(this.appFilePath, str.ToString(), Encoding.Default);
        else
          File.WriteAllText(this.appFilePath, str.ToString(), Encoding.Default);
      }
      catch (Exception ex)
      {
      }
    }

    public void makeRequestRepeat(JSonAlarmHistory setData)
    {
      try
      {
        string str = "FAULT|" + JsonConvert.SerializeObject((object) setData) + "\r\n";
        if (File.Exists(this.appFilePath))
          File.AppendAllText(this.appFilePath, str.ToString(), Encoding.Default);
        else
          File.WriteAllText(this.appFilePath, str.ToString(), Encoding.Default);
      }
      catch (Exception ex)
      {
      }
    }
  }
}
