using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PCRClient;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;

namespace PCRApi
{
    [JsonObject]
    internal class QueryResult
    {
        public DateTime time;
        public Guid request_id;
        public JToken result;
        public bool iserr;

    }

    [JsonObject]
    internal class QueryRequest
    {
        public long viewer_id;
        public Guid request_id;

    }

    [JsonObject]
    internal class Account
    {
        public string uid, access_key;
    }

    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ILogger<ApiController> _logger;

        private static readonly ConcurrentQueue<QueryResult> results = new ConcurrentQueue<QueryResult>();
        private static readonly ConcurrentDictionary<Guid, long> indexes = new ConcurrentDictionary<Guid, long>();
        private static readonly BlockingCollection<QueryRequest> requests = new BlockingCollection<QueryRequest>();
        private static int indexnow = 0;
        private static int nextindex = 0;

        public static void Dummy()
        {

        }

        static ApiController()
        {
            foreach (Account account in JsonConvert.DeserializeObject<Account[]>(System.IO.File.ReadAllText("accounts.json")))
                new Thread(new ThreadStart(() =>
                {
                    PCRClient.PCRClient client = new PCRClient.PCRClient(new EnvironmentInfo());

                    client.Login(account.uid, account.access_key);

                    while (true)
                    {
                        QueryRequest request = requests.Take();

                        callapi:
                        try
                        {
                            JToken data = client.Callapi("profile/get_profile", new JObject
                            {
                                ["target_viewer_id"] = request.viewer_id
                            }, noerr: true);

                            if (data?["server_error"]?.Value<int>("status") == 3)
                            {
                                client = new PCRClient.PCRClient(new EnvironmentInfo());
                                client.Login(account.uid, account.access_key);
                                goto callapi;
                            }

                            DateTime now = DateTime.Now;

                            results.Enqueue(new QueryResult
                            {
                                time = now,
                                request_id = request.request_id,
                                result = data
                            });

                            indexes.TryRemove(request.request_id, out long _);

                            while (results.Count > 10000)
                                results.TryDequeue(out QueryResult result);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }

                        ++indexnow;
                    }
                })).Start();
        }
        public ApiController(ILogger<ApiController> logger)
        {
            _logger = logger;

        }

        private IPEndPoint Source => new IPEndPoint(HttpContext.Connection.RemoteIpAddress, HttpContext.Connection.RemotePort);
        
        [HttpGet("enqueue")]
        public ActionResult<string> Enqueue(long target_viewer_id)
        {
            _logger.LogInformation($"[{Source}]api called /enqueue, target_viewer_id = {target_viewer_id}");
            if (requests.Count > 10000)
                return new JObject
                {
                    ["request_id"] = null,
                    ["message"] = "request queue count reached limit"
                }.ToString();

            Guid guid = Guid.NewGuid();
            indexes.TryAdd(guid, Interlocked.Increment(ref nextindex));

            requests.Add(new QueryRequest
            {
                request_id = guid,
                viewer_id = target_viewer_id
            });

            return new JObject
            {
                ["reqeust_id"] = guid
            }.ToString();
        }

        [JsonObject]
        private class UserInfo
        {
            public int arena_rank, grand_arena_rank;
        }

        [JsonObject]
        private class Profile
        {
            public UserInfo user_info;
        }

        [HttpGet("query")]
        public ActionResult<string> Query(Guid request_id, bool full)
        {
            _logger.LogInformation($"[{Source}]api called /query, request_id = {request_id}, full = {full}");
            if (indexes.TryGetValue(request_id, out long index))
            {
                long delta = indexnow - index;
                return new JObject
                {
                    ["status"] = "queue",
                    ["pos"] = -delta
                }.ToString();
            }

            QueryResult result = results.FirstOrDefault(result => result.request_id == request_id);

            if (!full && (result?.result != null))
                result.result = JToken.FromObject(result.result.ToObject<Profile>());

            if (result == null)
                return new JObject
                {
                    ["status"] = "notfound"
                }.ToString();
            else
                return new JObject
                {
                    ["status"] = "done",
                    ["data"] = result.result
                }.ToString();
        }
    }
}
