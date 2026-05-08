// Decompiled with JetBrains decompiler
// Type: EvComm.ResponseServer
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using System;

namespace EvChargerUI.Services.EvComm
{
  public class ResponseServer
  {
    private HttpAsyncServer serverReset;
    private HttpAsyncServer serverPrices;
    private HttpAsyncServer serverDisplayBrightness;
    private HttpAsyncServer serverSound;
    private HttpAsyncServer serverUpdate;
    private HttpAsyncServer serverStatus;
    private HttpAsyncServer serverCheckStatus;
    private HttpAsyncServer serverLimit;
    private HttpAsyncServer serverTest;
    private HttpAsyncServer serverPayYn;
    private HttpAsyncServer serverAuth;
    private HttpAsyncServer serverStop;
    private HttpAsyncServer serverDump;
    private string urlReset = "";
    private string urlPrices = "";
    private string urlDisplayBrightness = "";
    private string urlSound = "";
    private string urlUpdate = "";
    private string urlStatus = "";
    private string urlCheckStatus = "";
    private string urlLimit = "";
    private string urlTest = "";
    private string urlPayYn = "";
    private string urlAuth = "";
    private string urlStop = "";
    private string urlDump = "";
    private EvCommForm evComm;

    public ResponseServer() => this.SaveUrl();

    public HttpAsyncServer GetInstanceServerReset() => this.serverReset;

    public HttpAsyncServer GetInstanceServerPrices() => this.serverPrices;

    public HttpAsyncServer GetInstanceServerDisplayBrightness() => this.serverDisplayBrightness;

    public HttpAsyncServer GetInstanceServerSound() => this.serverSound;

    public HttpAsyncServer GetInstanceServerUpdate() => this.serverUpdate;

    public HttpAsyncServer GetInstanceServerStatus() => this.serverStatus;

    public HttpAsyncServer GetInstanceServerCheckStatus() => this.serverCheckStatus;

    public HttpAsyncServer GetInstanceServerLimit() => this.serverLimit;

    public HttpAsyncServer GetInstanceServerTest() => this.serverTest;

    public HttpAsyncServer GetInstanceServerPayYn() => this.serverPayYn;

    public HttpAsyncServer GetInstanceServerAuth() => this.serverAuth;

    public HttpAsyncServer GetInstanceServerStop() => this.serverStop;

    public HttpAsyncServer GetInstanceServerDump() => this.serverDump;

        public void OpenServer(EvCommForm evComm)
    {
      this.evComm = evComm;
      this.OpenServerReset();
      this.OpenServerPrices();
      this.OpenServerDisplayBrightness();
      this.OpenServerSound();
      this.OpenServerUpdate();
      this.OpenServerStatus();
      this.OpenServerCheckStatus();
      this.OpenServerLimit();
      this.OpenServerTest();
      this.OpenServerPayYn();
      this.OpenServerAuth();
      this.OpenServerStop();
      this.OpenServerDump();

        }

    public void CloseServer()
    {
      this.CloseServerReset();
      this.CloseServerPrices();
      this.CloseServerDisplayBrightness();
      this.CloseServerSound();
      this.CloseServerUpdate();
      this.CloseServerStatus();
      this.CloseServerCheckStatus();
      this.CloseServerLimit();
      this.CloseServerTest();
      this.CloseServerPayYn();
      this.CloseServerAuth();
      this.CloseServerStop();
      this.CloseServerDump();
        }

    private void SaveUrl()
    {
      this.urlReset = ResponseData.clientUrl + "charger/reset/";
      this.urlPrices = ResponseData.clientUrl + "charger/prices/";
      this.urlDisplayBrightness = ResponseData.clientUrl + "charger/displayBrightness/";
      this.urlSound = ResponseData.clientUrl + "charger/sound/";
      this.urlUpdate = ResponseData.clientUrl + "charger/update/";
      this.urlStatus = ResponseData.clientUrl + "charger/status/";
      this.urlCheckStatus = ResponseData.clientUrl + "charger/checkstatus/";
      this.urlLimit = ResponseData.clientUrl + "charger/limit/";
      this.urlTest = ResponseData.clientUrl + "charger/test/";
      this.urlPayYn = ResponseData.clientUrl + "charger/payyn/";
      this.urlAuth = ResponseData.clientUrl + "charger/auth/";
      this.urlStop = ResponseData.clientUrl + "charger/stop/";
      this.urlDump = ResponseData.clientUrl + "charger/dump/";
        }

    private void OpenServerReset()
    {
      this.serverReset = new HttpAsyncServer(new string[1]
      {
        this.urlReset
      }, this, "RESET", this.evComm);
      this.serverReset.RunServer();
    }

    private void CloseServerReset()
    {
      try
      {
        if (this.serverReset == null)
          return;
        this.serverReset.stop();
        this.serverReset = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerPrices()
    {
      this.serverPrices = new HttpAsyncServer(new string[1]
      {
        this.urlPrices
      }, this, "PRICES", this.evComm);
      this.serverPrices.RunServer();
    }

    private void CloseServerPrices()
    {
      try
      {
        if (this.serverPrices == null)
          return;
        this.serverPrices.stop();
        this.serverPrices = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerDisplayBrightness()
    {
      this.serverDisplayBrightness = new HttpAsyncServer(new string[1]
      {
        this.urlDisplayBrightness
      }, this, "DISPLAYBRIGHTNESS", this.evComm);
      this.serverDisplayBrightness.RunServer();
    }

    private void CloseServerDisplayBrightness()
    {
      try
      {
        if (this.serverDisplayBrightness == null)
          return;
        this.serverDisplayBrightness.stop();
        this.serverDisplayBrightness = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerSound()
    {
      this.serverSound = new HttpAsyncServer(new string[1]
      {
        this.urlSound
      }, this, "SOUND", this.evComm);
      this.serverSound.RunServer();
    }

    private void CloseServerSound()
    {
      try
      {
        if (this.serverSound == null)
          return;
        this.serverSound.stop();
        this.serverSound = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerUpdate()
    {
      this.serverUpdate = new HttpAsyncServer(new string[1]
      {
        this.urlUpdate
      }, this, "UPDATE", this.evComm);
      this.serverUpdate.RunServer();
    }

    private void CloseServerUpdate()
    {
      try
      {
        if (this.serverUpdate == null)
          return;
        this.serverUpdate.stop();
        this.serverUpdate = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerStatus()
    {
      this.serverStatus = new HttpAsyncServer(new string[1]
      {
        this.urlStatus
      }, this, "STATUS", this.evComm);
      this.serverStatus.RunServer();
    }

    private void CloseServerStatus()
    {
      try
      {
        if (this.serverStatus == null)
          return;
        this.serverStatus.stop();
        this.serverStatus = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerCheckStatus()
    {
      this.serverCheckStatus = new HttpAsyncServer(new string[1]
      {
        this.urlCheckStatus
      }, this, "CHECKSTATUS", this.evComm);
      this.serverCheckStatus.RunServer();
    }

    private void CloseServerCheckStatus()
    {
      try
      {
        if (this.serverCheckStatus == null)
          return;
        this.serverCheckStatus.stop();
        this.serverCheckStatus = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerLimit()
    {
      this.serverLimit = new HttpAsyncServer(new string[1]
      {
        this.urlLimit
      }, this, "LIMIT", this.evComm);
      this.serverLimit.RunServer();
    }

    private void CloseServerLimit()
    {
      try
      {
        if (this.serverLimit == null)
          return;
        this.serverLimit.stop();
        this.serverLimit = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerTest()
    {
      this.serverTest = new HttpAsyncServer(new string[1]
      {
        this.urlTest
      }, this, "TEST", this.evComm);
      this.serverTest.RunServer();
    }

    private void CloseServerTest()
    {
      try
      {
        if (this.serverTest == null)
          return;
        this.serverTest.stop();
        this.serverTest = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerPayYn()
    {
      this.serverPayYn = new HttpAsyncServer(new string[1]
      {
        this.urlPayYn
      }, this, "PAYYN", this.evComm);
      this.serverPayYn.RunServer();
    }

    private void CloseServerPayYn()
    {
      try
      {
        if (this.serverPayYn == null)
          return;
        this.serverPayYn.stop();
        this.serverPayYn = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerAuth()
    {
      this.serverAuth = new HttpAsyncServer(new string[1]
      {
        this.urlAuth
      }, this, "AUTH", this.evComm);
      this.serverAuth.RunServer();
    }

    private void CloseServerAuth()
    {
      try
      {
        if (this.serverAuth == null)
          return;
        this.serverAuth.stop();
        this.serverAuth = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

    private void OpenServerStop()
    {
      this.serverStop = new HttpAsyncServer(new string[1]
      {
        this.urlStop
      }, this, "STOP", this.evComm);
      this.serverStop.RunServer();
    }

    private void CloseServerStop()
    {
      try
      {
        if (this.serverStop == null)
          return;
        this.serverStop.stop();
        this.serverStop = (HttpAsyncServer) null;
      }
      catch (ObjectDisposedException ex)
      {
      }
    }

        private void OpenServerDump()
        {
            this.serverDump = new HttpAsyncServer(new string[1]
            {
        this.urlDump
            }, this, "DUMP", this.evComm);
            this.serverDump.RunServer();
        }

        private void CloseServerDump()
        {
            try
            {
                if (this.serverDump == null)
                    return;
                this.serverDump.stop();
                this.serverDump = (HttpAsyncServer)null;
            }
            catch (ObjectDisposedException ex)
            {
            }
        }
    }
}
