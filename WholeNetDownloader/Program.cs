using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace WholeNetDownloader
{
    class Program
    {
        static void Main(string[] args)
        {
            var WWWDownloader = new NetDownloader("https://www.delta-skola.cz/", "trash");

            Process.Start("trash");

            WWWDownloader.Download();

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
        private ConcurrentQueue<Uri> downloadQueue;
        private int runningDownloadsCount = 0;

        public NetDownloader(string startUrl, string directoryPath)
        {
            _startUri = new Uri(startUrl);
            directory = new DirectoryInfo(directoryPath);
        }

        private void NotifyUser()
        {
            if (_lastNotifySize + NotifyMemoryVar < DirSize(directory))
            {
                _lastNotifySize = DirSize(directory);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Stáhlo se již {0}KB", _lastNotifySize / 1024);
                Console.ResetColor();
            }
        }

        public void Download()
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

            downloadQueue = new ConcurrentQueue<Uri>();
            downloadQueue.Enqueue(_startUri);

            StartNewDownload();
        }

        private void StartNewDownload()
        {
            while (downloadQueue.Count > 0 || runningDownloadsCount > 0)
            {
                if (downloadQueue.TryDequeue(out Uri link))
                {
                    _ = DownloadPage(link);
                }
            }
        }

        async private Task DownloadPage(Uri uri)
        {
            NotifyUser();
            if (directory.GetFiles().Select(x => x.Name).Contains(GetFileNameFromUri(uri)))
            {
                return;
            }

            using (var webClient = new WebClient
            {
                Encoding = Encoding.UTF8
            }
            )
            {
                Console.WriteLine("{0}", uri.ToString());
                string downloadedStr;
                Interlocked.Increment(ref runningDownloadsCount);
                try
                {
                    downloadedStr = await webClient.DownloadStringTaskAsync(uri);
                    File.WriteAllText(Path.Combine(directory.FullName, GetFileNameFromUri(uri)), downloadedStr, Encoding.UTF8);
                }
                catch (Exception)
                {
                    return;
                }

                try
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(downloadedStr);

                    var body = doc.DocumentNode.SelectSingleNode("html/body");
                    if (body == null) return;

                    var addressNodes = body.SelectNodes(".//a");
                    if (addressNodes == null) return;


                    List<Uri> pageLinks = new List<Uri>();
                    foreach (var address in addressNodes)
                    {
                        if (address.Attributes.Contains("href") && address.Attributes["href"].Value.StartsWith("http"))
                        {
                            string addressHref = address.Attributes["href"].Value;
                            if (addressHref.Contains("?"))
                            {
                                addressHref = addressHref.Remove(addressHref.IndexOf("?"));
                            }
                            pageLinks.Add(new Uri(addressHref));
                        }
                    }

                    foreach (var link in pageLinks)
                    {
                        downloadQueue.Enqueue(link);
                    }
                    Interlocked.Decrement(ref runningDownloadsCount);
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
