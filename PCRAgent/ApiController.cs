using LitJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MsgPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace PCRAgent
{
    public class Ref<T>
    {
        public T value;
    }

    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ILogger<ApiController> _logger;
        private HttpClient client;
        private static TextWriter filelog;

        private void LogInformation(string info)
        {
            if (filelog == null) filelog = new StreamWriter(new FileStream("api.log", FileMode.Append, FileAccess.Write));
            filelog.WriteLine(info);
            filelog.Flush();
            _logger.LogInformation(info);
        }

        public ApiController(ILogger<ApiController> logger)
        {
            _logger = logger;
            WebRequest.DefaultWebProxy = null;
            client = new HttpClient(new HttpClientHandler
            {
                UseProxy = false,
                Proxy = null
            });
        }

        private byte[] TransformMsg(byte[] data, Func<JObject, JObject> trans)
        {
            try
            {
                RijndaelManaged aes = new RijndaelManaged
                {
                    Mode = CipherMode.CBC,
                    KeySize = 256,
                    BlockSize = 128,
                    Padding = PaddingMode.PKCS7
                };

                int n = data.Length;

                byte[] key = data.Skip(n - 32).ToArray();

                ICryptoTransform transform = aes.CreateDecryptor(key, Encoding.UTF8.GetBytes("ha4nBYA2APUD6Uv1"));
                ICryptoTransform transform2 = aes.CreateEncryptor(key, Encoding.UTF8.GetBytes("ha4nBYA2APUD6Uv1"));

                byte[] buf = transform.TransformFinalBlock(data.Take(n - 32).ToArray(), 0, n - 32);

                JObject obj = JObject.Parse(JsonMapper.ToJson(new BoxingPacker().Unpack(buf)));
                
                obj = trans(obj);

                Func<JToken, object> mapper = null;

                mapper = o =>
                {
                    if (o is JArray arr) return arr.Select(mapper).ToArray();
                    else if (o is JObject obj) return new Dictionary<string, object>(
                        obj.Properties().Select(prop => new KeyValuePair<string, object>(prop.Name, mapper(prop.Value)))
                    );
                    else return o.ToObject<object>();
                };

                object mapped = mapper(obj);

                buf = new BoxingPacker().Pack(mapped);

                return transform2.TransformFinalBlock(buf, 0, buf.Length).Concat(key).ToArray();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e.ToString());
                return data;
            }
        }

        private string TransformMsg(string data, Func<JObject, JObject> trans)
        {
            try
            {
                return Convert.ToBase64String(TransformMsg(Convert.FromBase64String(data), trans));
            }
            catch
            {
                return data;
            }
        }

        public static string Unpack(byte[] crypted, out byte[] key)
        {

            RijndaelManaged aes = new RijndaelManaged
            {
                Mode = CipherMode.CBC,
                KeySize = 256,
                BlockSize = 128,
                Padding = PaddingMode.PKCS7
            };
            int n = crypted.Length;
            key = crypted.Skip(n - 32).ToArray();
            ICryptoTransform transform = aes.CreateDecryptor(key, Encoding.UTF8.GetBytes("ha4nBYA2APUD6Uv1"));
            byte[] buf = transform.TransformFinalBlock(crypted.Take(n - 32).ToArray(), 0, n - 32);
            return Encoding.UTF8.GetString(buf);
        }


        [HttpPost("{route}/{method}")]
        public ActionResult<string> Post(string route, string method)
        {
            StreamReader sr = new StreamReader(Request.Body);
            byte[] reqdata = Convert.FromBase64String(JObject.Parse(sr.ReadToEnd())["data"].ToString());

            if (method != "index")
            {

                client = new HttpClient(new HttpClientHandler
                {
                    UseProxy = false
                });
            }
            client.DefaultRequestHeaders.Clear();

            var headerIgnores = new HashSet<string>() { "Host" };

            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in Request.Headers)
                if (!headerIgnores.Contains(header.Key))
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, (string[])header.Value);
             
            if (route == "tool" && method == "sdk_login")
            {
                LogInformation($"calling api {route}/{method}");


                reqdata = TransformMsg(reqdata, obj =>
                {
                    if (obj.ContainsKey("message_id")) obj["message_id"] = 1;
                    LogInformation($"request = {JsonConvert.SerializeObject(obj, Formatting.Indented)}");
                    return obj;
                });

                return NotFound();
            }

            HttpResponseMessage response = client.PostAsync($"http://l3-prod-all-gs-gzlj.bilibiligame.net/{route}/{method}{Request.QueryString}", new ByteArrayContent(reqdata)).Result;
            string respdata = response.Content.ReadAsStringAsync().Result;

            Response.Headers.Clear();

            return new ActionResult<string>(respdata);
        }
    }
}
