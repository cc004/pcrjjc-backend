using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace PCRClient
{
    public class PCRClient
    {
        private readonly HttpClient client;
        private const string urlroot = "http://l3-prod-all-gs-gzlj.bilibiligame.net/";
        private long viewer_id;
        private string request_id;
        private string session_id;
        private readonly EnvironmentInfo environment;
        public JObject Load { get; private set; }
        public JObject Home { get; private set; }

        public int ClanId => Home["user_clan"].Value<int>("clan_id");

        public PCRClient(EnvironmentInfo info)
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            foreach (System.Reflection.FieldInfo field in typeof(EnvironmentInfo).GetFields())
            {
                if (field.FieldType != typeof(string)) continue;
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    field.IsDefined(typeof(NoUpperAttribute), true) ?
                        field.Name.Replace('_', '-') : 
                        field.Name.Replace('_', '-').ToUpper(),
                    field.GetValue(info) as string);
            }

            client.DefaultRequestHeaders.TryAddWithoutValidation("Connection", "Keep-Alive");

            environment = info;
            viewer_id = info.viewer_id;
        }

        public JToken Callapi(string apiurl, JObject request, bool crypted = true, bool noerr=false)
        {
            int startTime = Environment.TickCount;
            string reqs = request.ToString();

            byte[] key = PackHelper.CreateKey();
            request.Add("viewer_id", crypted ? PackHelper.Encrypt(viewer_id.ToString(), key) : viewer_id.ToString());
            byte[] req = PackHelper.Pack(request, key);

            bool flag = request_id != null, flag2 = session_id != null;
            if (flag) client.DefaultRequestHeaders.TryAddWithoutValidation("REQUEST-ID", request_id);
            if (flag2) client.DefaultRequestHeaders.TryAddWithoutValidation("SID", session_id);

            int startTime1 = Environment.TickCount;
            HttpResponseMessage resp = client.PostAsync(urlroot + apiurl, crypted ? new ByteArrayContent(req) : new StringContent(request.ToString())).Result;
            int endTime1 = Environment.TickCount;

            if (flag) client.DefaultRequestHeaders.Remove("REQUEST-ID");
            if (flag2) client.DefaultRequestHeaders.Remove("SID");

            string respdata = resp.Content.ReadAsStringAsync().Result;
            JToken json = crypted ? PackHelper.Unpack(Convert.FromBase64String(respdata), out byte[] _) : JObject.Parse(respdata);

            JObject header = json["data_headers"] as JObject;
            if (header.TryGetValue("sid", out JToken sid) && !string.IsNullOrEmpty((string)sid))
            {
                using (MD5 md5 = MD5.Create())
                    session_id = string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes((string)sid + "c!SID!n")).Select(b => b.ToString("x2")));
                //Console.WriteLine("switching session_id to " + session_id);
            }

            if (header.TryGetValue("request_id", out JToken rid) && (string)rid != request_id)
            {
                request_id = (string)rid;
                //Console.WriteLine("switching request_id to " + request_id);
            }

            if (header.TryGetValue("viewer_id", out JToken vid) && (long?)vid != null && (long)vid != viewer_id)
            {
                viewer_id = (long)vid;
                //Console.WriteLine("switching viewer_id to " + viewer_id);
            }

            if (json["data"] is JObject obj)
                if (!noerr && obj.TryGetValue("server_error", out JToken obj2))
                    throw new ApiException($"{obj2["title"]}: {obj2["message"]} (code = {obj2["status"]})");

            //Console.WriteLine(json["data"]);

            Console.WriteLine($"called api {apiurl}, {Environment.TickCount - startTime}ms ({endTime1 - startTime1}ms) elapsed");

            return json["data"];
        }

        public void Login(string uid, string access_key)
        {
            JToken manifest = Callapi("source_ini/get_maintenance_status?format=json", new JObject(), false);
            string ver = (string)manifest["required_manifest_ver"];
            Console.WriteLine($"using manifest: " + ver);
            client.DefaultRequestHeaders.TryAddWithoutValidation("MANIFEST-VER", ver);

            Console.WriteLine($"Logging in with uid = {uid}, access_key = {access_key}");
            Callapi("tool/sdk_login", new JObject
            {
                ["uid"] = uid,
                ["access_key"] = access_key,
                ["channel"] = environment.channel_id,
                ["platform"] = environment.platform_id
            });

            Callapi("check/game_start", new JObject
            {
                ["app_type"] = 0,
                ["campaign_data"] = "",
                ["campaign_user"] = new Random().Next(0, 100000),
            });

            Callapi("check/check_agreement", new JObject());

    
            Console.WriteLine("Requesting initial data...");
            Load = Callapi("load/index", new JObject { ["carrier"] = "OPPO" }) as JObject;
            Home = Callapi("home/index", new JObject
            {
                ["message_id"] = 1,
                ["tips_id_list"] = new JArray(),
                ["is_first"] = 1,
                ["gold_history"] = 0
            }) as JObject;
            Console.WriteLine("Logged in.");
        }
    }
}
