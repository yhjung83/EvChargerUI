// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.DataParser
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using System;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class DataParser
  {
    public bool CheckYN(string payYn)
    {
      try
      {
        switch (payYn)
        {
          case "Y":
            return true;
          case "N":
            return false;
          default:
            return false;
        }
      }
      catch (NullReferenceException ex)
      {
        return false;
      }
    }
  }
}
