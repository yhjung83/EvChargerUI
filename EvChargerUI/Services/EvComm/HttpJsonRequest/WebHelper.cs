// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.WebHelper
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class WebHelper
  {
    public string requestResponse = string.Empty;
    public static AutoResetEvent DoReset = new AutoResetEvent(false);
    public string resvInfoErrorCode = string.Empty;
    public string response_receive = string.Empty;

    public JObject Post(string messageType, Uri url, string value, int timeout)
    {
      HttpWebRequest state = (HttpWebRequest) WebRequest.Create(url);
      byte[] bytes = Encoding.ASCII.GetBytes(value);
      state.ContentType = "application/json";
      state.Method = "POST";
      state.ReadWriteTimeout = timeout;
      state.Timeout = timeout;
      try
      {
        JObject jobject = (JObject) null;
        using (Stream requestStream = state.GetRequestStream())
          requestStream.Write(bytes, 0, bytes.Length);
        state.BeginGetResponse(new AsyncCallback(this.GetResponsetStreamCallback), (object) state);
        WebHelper.DoReset.WaitOne();
        return this.requestResponse != null ? (jobject = JObject.Parse(this.requestResponse)) : (JObject) null;
      }
      catch (WebException ex)
      {
        return (JObject) null;
      }
      catch (JsonReaderException ex)
      {
        return (JObject) null;
      }
    }

    public JObject Get(Uri url, int timeout)
    {
      WebRequest webRequest = WebRequest.Create(url);
      webRequest.ContentType = "applicaiton/json";
      webRequest.Method = "GET";
      webRequest.Timeout = timeout;
      try
      {
        return (JObject) JObject.Parse(new StreamReader(webRequest.GetResponse().GetResponseStream()).ReadToEnd());
      }
      catch (WebException ex)
      {
        return (JObject) null;
      }
    }

    public object[] Download_Update(Uri url, string fileName, string ver_kind)
    {
      string str1 = "C:\\NANDFlash\\";
      object[] objArray = new object[2];
      string str2 = "0";
      try
      {
        WebClient webClient = new WebClient();
        webClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 2.0.50727; .NET CLR 3.0.04506.590; .NET CLR 3.5.20706; .NET CLR 3.0.04506.648; .NET CLR 3.5.21022; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729;)");
        webClient.Credentials = (ICredentials) new NetworkCredential("sitemgr", "S2_.T8UJ");
        webClient.UseDefaultCredentials = true;
        webClient.DownloadFileAsync(url, str1 + fileName);
        while (webClient.IsBusy)
        {
          Application.DoEvents();
          Thread.Sleep(1);
        }
        long int64 = Convert.ToInt64(webClient.ResponseHeaders["Content-Length"]);
        str2 = int64.ToString();
        objArray[0] = (object) str2;
        if (int64 == 0L)
        {
          objArray[1] = (object) false;
          return objArray;
        }
        objArray[1] = (object) true;
        return objArray;
      }
      catch (Exception ex)
      {
        objArray[0] = (object) str2;
        objArray[1] = (object) false;
        return objArray;
      }
    }

    public static bool WebExists(string url)
    {
      bool flag = true;
      if (url == null)
        return false;
      HttpWebResponse httpWebResponse = (HttpWebResponse) null;
      try
      {
        HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create(url);
        httpWebRequest.Method = "HEAD";
        httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse();
      }
      catch (WebException ex)
      {
        flag = false;
      }
      finally
      {
        httpWebResponse?.Close();
      }
      return flag;
    }

    private void GetResponsetStreamCallback(IAsyncResult callbackResult)
    {
      try
      {
        HttpWebResponse response = (HttpWebResponse) ((WebRequest) callbackResult.AsyncState).EndGetResponse(callbackResult);
        Stream responseStream = response.GetResponseStream();
        StreamReader streamReader = new StreamReader(responseStream);
        string end = streamReader.ReadToEnd();
        responseStream.Close();
        streamReader.Close();
        response.Close();
        this.requestResponse = end;
      }
      catch (Exception ex)
      {
        this.requestResponse = (string) null;
      }
      WebHelper.DoReset.Set();
    }
  }
}
