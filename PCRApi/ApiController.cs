using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PCRClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace PCRApi
{
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
        [JsonObject]
        private class QueryResult
        {
            public Profile result;
            public long index;

        }

        private static readonly Queue<Guid> uids = new Queue<Guid>();
        private static readonly Dictionary<Guid, QueryResult> results = new Dictionary<Guid, QueryResult>();
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

                    while (true)
                    {
                        QueryRequest request;
                        request = requests.Take();

                        try
                        {
                            JToken data = client.Callapi("profile/get_profile", new JObject
                            {
                                ["target_viewer_id"] = request.viewer_id
                            }, noerr: true);

                            if (data?["server_error"]?.Value<int>("status") == 3)
                            {
                                lock (results)
                                    results.Remove(request.request_id);
                                client = new PCRClient.PCRClient(new EnvironmentInfo());
                                client.Login(account.uid, account.access_key);
                            }
                            else
                            {
                                DateTime now = DateTime.Now;

                                lock (results)
                                {
                                    results[request.request_id].result = data.ToObject<Profile>();

                                    while (uids.Count > 1000)
                                        results.Remove(uids.Dequeue());
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }

                        ++indexnow;
                    }
                })).Start();
        }

        private IPEndPoint Source => new IPEndPoint(HttpContext.Connection.RemoteIpAddress, HttpContext.Connection.RemotePort);
        
        [HttpGet("enqueue")]
        public ActionResult<string> Enqueue(long target_viewer_id)
        {
            //_logger.LogInformation($"[{Source}]api called /enqueue, target_viewer_id = {target_viewer_id}");
            if (requests.Count > 10000)
                return new JObject
                {
                    ["request_id"] = null,
                    ["message"] = "request queue count reached limit"
                }.ToString();

            Guid guid = Guid.NewGuid();
            lock (results)
            {
                results.Add(guid, new QueryResult
                {
                    index = Interlocked.Increment(ref nextindex)
                });
                uids.Enqueue(guid);
            }

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
            if (!results.TryGetValue(request_id, out var val))
            {
                return new JObject
                {
                    ["status"] = "notfound"
                }.ToString();
            }
            else if (val.result == null)
            {
                long delta = indexnow - val.index;
                return new JObject
                {
                    ["status"] = "queue",
                    ["pos"] = -delta
                }.ToString();
            }
            else
            {
                return new JObject
                {
                    ["status"] = "done",
                    ["data"] = JToken.FromObject(val.result)
                }.ToString();
            }
        }
    }
}
