namespace CloudNotes.DesktopClient.Extensions.Blog.MetaWeblogSharp.XmlRPC
{
	using System.IO;
	using System.Net;
	using System.Text;
	using System.Threading.Tasks;
	using System.Xml.Linq;

	public class Service
	{
		private bool EnableExpect100Continue = false;
		public CookieContainer Cookies = null;
		public string URL
		{
			get;
			private set;
		}
		public Service(string url)
		{
			this.URL = url;
		}
        //public MethodResponse Execute(MethodCall methodcall)
        //{
        //    XDocument xDocument = methodcall.CreateDocument();
        //    WebRequest webRequest = WebRequest.Create(this.URL);
        //    if (this.Cookies != null)
        //    {
        //        HttpWebRequest httpWebRequest = webRequest as HttpWebRequest;
        //        httpWebRequest.CookieContainer = this.Cookies;
        //    }
        //    HttpWebRequest httpWebRequest2 = (HttpWebRequest)webRequest;
        //    httpWebRequest2.ServicePoint.Expect100Continue = this.EnableExpect100Continue;
        //    webRequest.Method = "POST";
        //    string s = xDocument.ToString();
        //    byte[] bytes = Encoding.UTF8.GetBytes(s);
        //    webRequest.ContentType = "text/xml;charset=utf-8";
        //    webRequest.ContentLength = (long)bytes.Length;
        //    using (Stream requestStream = webRequest.GetRequestStream())
        //    {
        //        requestStream.Write(bytes, 0, bytes.Length);
        //    }
        //    MethodResponse result;
        //    using (HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse())
        //    {
        //        using (Stream responseStream = httpWebResponse.GetResponseStream())
        //        {
        //            if (responseStream == null)
        //            {
        //                throw new XmlRPCException("Response Stream is unexpectedly null");
        //            }
        //            using (StreamReader streamReader = new StreamReader(responseStream))
        //            {
        //                string content = streamReader.ReadToEnd();
        //                MethodResponse methodResponse = new MethodResponse(content);
        //                result = methodResponse;
        //            }
        //        }
        //    }
        //    return result;
        //}

	    public async Task<MethodResponse> ExecuteAsync(MethodCall methodCall)
	    {
	        var xDocument = methodCall.CreateDocument();
            var webRequest = WebRequest.Create(this.URL);
            if (this.Cookies != null)
            {
                var httpWebRequest = webRequest as HttpWebRequest;
                if (httpWebRequest != null)
                {
                    httpWebRequest.CookieContainer = this.Cookies;
                }
            }
            var httpWebRequest2 = (HttpWebRequest)webRequest;
            httpWebRequest2.ServicePoint.Expect100Continue = this.EnableExpect100Continue;
            webRequest.Method = "POST";
            var s = xDocument.ToString();
            var bytes = Encoding.UTF8.GetBytes(s);
            webRequest.ContentType = "text/xml;charset=utf-8";
            webRequest.ContentLength = bytes.Length;
            using (var requestStream = await webRequest.GetRequestStreamAsync())
            {
                await requestStream.WriteAsync(bytes, 0, bytes.Length);
            }
            MethodResponse result;
            using (var httpWebResponse = (HttpWebResponse)await webRequest.GetResponseAsync())
            {
                using (var responseStream = httpWebResponse.GetResponseStream())
                {
                    if (responseStream == null)
                    {
                        throw new XmlRPCException("Response Stream is unexpectedly null");
                    }
                    using (StreamReader streamReader = new StreamReader(responseStream))
                    {
                        string content = await streamReader.ReadToEndAsync();
                        var methodResponse = new MethodResponse(content);
                        result = methodResponse;
                    }
                }
            }
            return result;
	    }
	}
}
