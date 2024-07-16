using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AutoReserve
{
    public class Util
    {
        public static async Task<string> WebPostJsonSend(string url, object obj, Dictionary<string, string> dicRequestHeader = null)
        {

            string strResult = string.Empty;

            WebRequest webRequest;

            webRequest = WebRequest.Create(url);
            webRequest.Method = "POST";


            byte[] tempData = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(obj));

            webRequest.ContentType = "application/json; charset=UTF-8";
            webRequest.ContentLength = tempData.Length;

            if (dicRequestHeader != null)
            {
                dicRequestHeader.ToList().ForEach(x =>
                {
                    webRequest.Headers.Add(x.Key, x.Value);
                });
            }

            using (Stream stream = await webRequest.GetRequestStreamAsync())
            {
                await stream.WriteAsync(tempData, 0, tempData.Length);

                var response = await webRequest.GetResponseAsync();
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(responseStream, Encoding.UTF8))
                    {
                        strResult = reader.ReadToEnd();
                    }
                }
            }

            return strResult;
        }
    }

    public abstract class MessageSender
    {
        public abstract Task Send(string msg, string url = null, string imgUrl = null);
    }

    public class Slack : MessageSender
    {
        private string WebhookUrl { get; set; }
        public Slack(string webhookUrl)
        {
            WebhookUrl = webhookUrl;
        }
        public override async Task Send(string msg, string url = null, string imgUrl = null)
        {
            if ((url ?? "").Length > 0)
            {
                msg += $"\r\n <${ url}| 여기를 클릭>";
            }

            var objMsg = new
            {
                blocks = new List<dynamic> {
                    new {
                        type = "section",
                        text =new { type= "mrkdwn", text= msg }
                    }
                }
            };

            if ((imgUrl ?? "").Length > 0)
            {
                objMsg.blocks[0].accessory = new
                {
                    type = "image",
                    image_url = imgUrl,
                    alt_text = ""
                };
            }

            await Util.WebPostJsonSend(WebhookUrl, objMsg);
        }
    }
}
