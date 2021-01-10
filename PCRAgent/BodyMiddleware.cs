using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCRAgent
{
    public class BodyMiddleware
    {
        private readonly RequestDelegate _next;
        public static byte[] data;

        public BodyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            IEnumerable<byte> requestContent = new byte[0];
            int b;

            do
            {
                byte[] t = new byte[1024];
                b = await context.Request.Body.ReadAsync(t, 0, 1024);
                if (b > 0)
                    requestContent = requestContent.Concat(t.Take(b));
            } while (b == 1024);

            data = requestContent.ToArray();

            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new JObject
            {
                ["data"] = Convert.ToBase64String(data)
            }.ToString()));

            await _next(context);

        }
    }
}
