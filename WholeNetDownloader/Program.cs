using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace WholeNetDownloader
{
    class Program
    {
        async static Task Main(string[] args)
        {
            var WWWDownloader = new NetDownloader("https://www.delta-skola.cz/", "trash");

            Process.Start("trash");

            await WWWDownloader.Download();

            Console.WriteLine("Work Finished");
            Console.ReadKey();
        }
    }

    class NetDownloader
    {
        public long NotifyMemoryVar { get; set; } = 1024 * 512;     //512 KB
        private long _lastNotifySize = 0;
        private DirectoryInfo directory;
        private Uri _startUri;

        public NetDownloader(string startUrl, string directoryPath)
        {
            _startUri = new Uri(startUrl);
            directory = new DirectoryInfo(directoryPath);
        }

        async public Task Download()
        {
            if (directory.Exists)
            {
                foreach (var file in directory.GetFiles())
                {
                    file.Delete();
                }
            }
            else
            {
                Directory.CreateDirectory(directory.FullName);
            }
            await DownloadPage(_startUri);
        }

        async private Task DownloadPage(Uri uri)
        {
            if (directory.GetFiles().Select(x => x.Name).Contains(GetFileNameFromUri(uri)))
            {
                return;
            }
            if (_lastNotifySize + NotifyMemoryVar < DirSize(directory))
            {
                _lastNotifySize = DirSize(directory);
                Console.WriteLine("Stáhlo se již {0}KB", _lastNotifySize / 1024);
            }

            using (var webClient = new WebClient
            {
                Encoding = Encoding.UTF8
            }
            )
            {
                Console.Write("Pokouším se o stažení {0}", uri.ToString());
                string downloadedStr;
                try
                {
                    downloadedStr = await webClient.DownloadStringTaskAsync(uri);
                    File.WriteAllText(Path.Combine(directory.FullName, GetFileNameFromUri(uri)), downloadedStr, Encoding.UTF8);
                    Console.WriteLine(" OK");
                }
                catch (Exception e)
                {
                    Console.WriteLine(" FAIL");
                    return;
                }

                try
                {
                    // Parsnout, najít další odkazy, rekurze
                    var doc = new HtmlDocument();
                    doc.LoadHtml(downloadedStr);

                    var body = doc.DocumentNode.SelectSingleNode("html/body");
                    if (body != null)
                    {
                        var nodes = body.SelectNodes(".//a");
                        if (nodes == null)
                        {
                            return;
                        }
                        foreach (var link in nodes)
                        {
                            if (link.Attributes.Contains("href") && link.Attributes["href"].Value.StartsWith("http"))
                            {
                                string address = link.Attributes["href"].Value;
                                if (address.Contains("?"))
                                {
                                    address = address.Remove(address.IndexOf("?"));
                                }
                                await DownloadPage(new Uri(address));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    //xd
                    Console.WriteLine(e.Message);
                }
            }
        }

        public static long DirSize(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += DirSize(di);
            }
            return size;
        }

        private string GetFileNameFromUri(Uri uri)
        {
            return uri.ToString()
                    .Replace("/", "_")
                    .Replace("\\", "")
                    .Replace(";", "")
                    .Replace("?", "")
                    .Replace(":", "") + ".page";
        }
    }
}
