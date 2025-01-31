﻿namespace Wikidown
{
    using System.Net.Http;
    using System.IO;
    using Newtonsoft.Json;

    internal class Program
    {
        static readonly HttpClient client = new();

        static async Task Main()
        {
            Console.Title = "Wikidown";

            Console.WriteLine("Enter arguments for page content. The prop parsewarnings will not be saved if empty, but will warn you if not empty, so it's recommended. This app is not meant to work with deprecated options. (utf8=true&prop=wikitext|categories|revid|properties|parsewarnings)");
            string? pageContentArguments = Console.ReadLine();
            if (pageContentArguments == null || pageContentArguments == "") pageContentArguments = "utf8=true&prop=wikitext|categories|revid|properties|parsewarnings";

            Console.WriteLine("Enter the base wiki URLs and prefixes. Ex: en.wikipedia.org:Prefix,incubator.wikimedia.org:Wp/ess/.");
            string? parameters = Console.ReadLine();
            parameters ??= "";
            string[] parameterPairs = parameters.Split(',');

            Directory.CreateDirectory("output");

            for (int i = 0; i < parameterPairs.Length; i++)
            {
                if (i != 0)
                {
                    Console.Write("\r" + parameterPairs[i - 1] + " - completed    \n");
                }

                string baseURL;
                string prefix;
                if (parameterPairs[i].Contains(':'))
                {
                    string[] splitParameterPair = parameterPairs[i].Split(':', 2);
                    baseURL = splitParameterPair[0];
                    prefix = splitParameterPair[1];
                }
                else
                {
                    baseURL = "incubator.wikimedia.org";
                    prefix = parameterPairs[i];
                }

                Directory.CreateDirectory("output/" + baseURL + "/" + prefix.Replace('/', '-'));
                Console.Write(parameterPairs[i] + " - listing pages");

                await DownloadPages();
                async Task DownloadPages(string? apcontinue = null)
                {
                    var listArguments = "action=query&format=json&list=allpages&aplimit=max&apfilterredir=nonredirects&apprefix=" + prefix;
                    if (apcontinue != null) listArguments += "&apcontinue=" + apcontinue;
                    var pages = await ReadWiki(baseURL, listArguments);
                    dynamic pagesJSON;
                    dynamic? nullablePagesJSON = JsonConvert.DeserializeObject(pages);
                    if (nullablePagesJSON == null || nullablePagesJSON is string)
                    {
                        Console.BackgroundColor = ConsoleColor.Red;
                        Console.Write("\nFatal error: Attempt to read wikipedia API returned invalid value: " + pages + "\n");
                        Console.ResetColor();
                        return;
                    }
                    pagesJSON = nullablePagesJSON;
                    Console.Write("\r" + parameterPairs[i] + " - downloading  ");
                    Console.CursorLeft -= 2;

                    foreach (dynamic page in pagesJSON.query.allpages)
                    {
                        var pageData = (await ReadWiki(baseURL, "action=parse&format=json&pageid=" + page.pageid + "&" + pageContentArguments)).Replace("\\n", "\n");
                        if (pageContentArguments.Contains("parsewarnings"))
                        {
                            if (pageData.Contains("\"parsewarnings\":[],"))
                            {
                                pageData = pageData.Replace("\"parsewarnings\":[],", "");
                            }
                            else
                            {
                                Console.BackgroundColor = ConsoleColor.Yellow;
                                Console.ForegroundColor = ConsoleColor.Black;
                                Console.Write($"\nWarning: page {page.title}({page.pageid}) seems to contain parse warnings. See the it's file for their content.\n");
                                Console.ResetColor();
                            }
                        }
                        try
                        {
                            var filename = "output/" + baseURL + "/" + prefix.Replace('/', '-') + "/" + ((string)page.title).Replace('/', '-') + ".json";
                            if (File.Exists(filename)) throw new();
                            File.WriteAllText(filename, pageData);
                        }
                        catch
                        {
                            try
                            {
                                var filename = "output/" + baseURL + "/" + prefix.Replace('/', '-') + "/" + page.pageid + ".json";
                                if (File.Exists(filename)) throw new("File already exists.");
                                File.WriteAllText(filename, pageData);
                            }
                            catch (Exception e)
                            {
                                Console.BackgroundColor = ConsoleColor.Red;
                                Console.Write("\nError: Didn't download page with the id " + page.pageid + ": " + e.Message + ". Continuing download of further pages.\n");
                                Console.ResetColor();
                            }
                        }
                    }

                    if (pagesJSON["continue"] != null) await DownloadPages(pagesJSON["continue"].apcontinue);
                }
            }

            Console.Write("\r" + parameterPairs[^1] + " - completed    ");
            Console.CursorLeft -= 4;
            Console.WriteLine("\nDownloaded all requested pages.");
            Console.ReadLine();
        }

        static async Task<string> ReadWiki(string baseURL, string apiOptions)
        {
            string url = "https://" + baseURL + "/w/api.php?" + apiOptions;
            
            try
            {
                return await client.GetStringAsync(url);
            }
            catch (Exception e)
            {
                return "Couldn't connect to \"" + url + "\": " + e.Message;
            }
        }
    }
}
