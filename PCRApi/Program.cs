using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NLog.Web;
using System.IO;

namespace PCRApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(File.ReadAllText("hosts.txt").Split("\r\n".ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries));
                    webBuilder.UseStartup<Startup>();
                }).UseNLog();
        }
    }
}
