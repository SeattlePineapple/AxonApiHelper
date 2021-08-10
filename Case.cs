using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AxonApiHelper
{
    public class Case
    {
        public Guid ID;
        public string Title;
        public string Description;
        public string Categories;
        public string Tags;
        public DateTime CreatedOn;
        public DateTime LastModified;
        public User LastModifiedBy;
        public User Owner;
        public string Status;
        public List<Evidence> Evidences;
        public List<Folder> Folders;

        public Case(Guid id)
        {
            ID = id;
        }

        public static List<Case> Search(string titleContains = null, CaseStatus status = CaseStatus.any)
        {
            Console.WriteLine("Searching for case");
            string titleParam = string.IsNullOrEmpty(titleContains) ? string.Empty : $"caseTitle={titleContains}";
            string statusParam = status == CaseStatus.any ? string.Empty : $"caseStatus={Enum.GetName(typeof(CaseStatus), status)}";
            List<string> paramList = new() { titleParam, statusParam };
            paramList.RemoveAll((a) => { return a.Equals(string.Empty); });
            List<Case> baseCases = Parse(Endpoint.Get($"cases{(paramList.Count == 0 ? string.Empty : "?" + string.Join('&', paramList))}", 2));
            List<Case> updatedCases = new(0);
            foreach (Case c in baseCases)
            {
                updatedCases.AddRange(Parse(Endpoint.Get($"cases/{c.ID}"))); // re-fetch each case individually to get all data points. only minimal fields are returned on initial search.
            }
            return updatedCases;
        }

        public void AddEvidence(Evidence e)
        {
            AddEvidence(new List<Evidence>() { e });
        }

        public void AddEvidence(List<Evidence> evidences)
        {
            if (evidences.Count > 10) // not sure what the limit is, but there is a POST content length limit
            {
                List<List<Evidence>> superList = evidences.Split(10);
                foreach (List<Evidence> subList in superList)
                {
                    AddEvidence(subList);
                }
            }
            List<string> contentItems = new();
            foreach (Evidence e in evidences)
            {
                contentItems.Add($"{{\"type\":\"evidence\",\"id\":\"{e.ID}\"}}");
            }
            string content = $"{{\"data\":[{string.Join(',', contentItems)}]}}";
            string result = Endpoint.Post($"cases/{ID}/relationships/evidence", content, 2);
            if (result.Equals(content))
            {
                Logger.Log($"Successfully added evidence to case \"{Title}\": {string.Join(", ", evidences.Select(e => $"\"{e.Title} ({e.ID})\""))}");
            }
            else
            {
                Logger.Log("Error adding evidence: " + result);
            }
        }

        public void GetEvidence()
        {
            Console.WriteLine("Enumerating Evidence");
            Evidences = Evidence.FromCase(ID);
        }

        private static List<Case> Parse(string json)
        {
            List<Case> cases = new();
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            JsonElement data = root.GetProperty("data");
            if (data.ValueKind == JsonValueKind.Array) // /cases?
            {
                foreach (JsonElement element in data.EnumerateArray())
                {
                    cases.Add(FromJsonElement(element));
                }
            }
            else // /cases/ID
            {
                cases.Add(FromJsonElement(data));
            }

            return cases;
        }

        private static Case FromJsonElement(JsonElement element)
        {
            Case c = new(element.TryGetProperty("id", typeof(Guid)));

            JsonElement attributes = element.GetProperty("attributes");

            c.Title = attributes.TryGetProperty("title", typeof(string));
            c.Description = attributes.TryGetProperty("description", typeof(string));
            c.CreatedOn = attributes.TryGetProperty("createdOn", typeof(DateTime));
            c.LastModified = attributes.TryGetProperty("lastModified", typeof(DateTime));
            c.Status = attributes.TryGetProperty("status", typeof(string));

            if (element.TryGetProperty("relationships", out JsonElement relationships))
            {
                if (relationships.TryGetProperty("folders", out JsonElement folders))
                {
                    if (c.Folders == null)
                    {
                        c.Folders = new();
                    }
                    if (folders.TryGetProperty("data", out JsonElement folderData))
                    {
                        if (folderData.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement f in folderData.EnumerateArray())
                            {
                                Folder folder = Folder.FromJsonElement(f);
                                folder.Case = c;
                                c.Folders.Add(folder);
                            }
                        }
                        else
                        {
                            Folder folder = Folder.FromJsonElement(folderData);
                            folder.Case = c;
                            c.Folders.Add(folder);
                        }
                    }
                }
            }

            return c;
        }

        public void AddFolder(string folderName)
        {
            string content = $@"
            {{
                ""data"":[
                    {{
                        ""type"":""folder"",
                        ""attributes"":{{ ""name"":""{folderName}""}}
                    }}
                ]
            }}";
            string response = Endpoint.Post($"cases/{ID}/relationships/folders", content, 2);
            using JsonDocument document = JsonDocument.Parse(response);
            JsonElement root = document.RootElement;
            JsonElement data = root.GetProperty("data");
            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement d in data.EnumerateArray())
                {
                    Folder f = Folder.FromJsonElement(d);
                    f.Case = this;
                    if (f.Name.Equals(folderName))
                    {
                        Folders.Add(f);
                    }
                }
            }
            else
            {
                Folder f = Folder.FromJsonElement(data);
                f.Case = this;
                Folders.Add(f);
            }
        }

        public static void DownloadEvidence(List<Evidence> evidences, string directory = @"C:\temp\files")
        {
            Console.WriteLine($"Downloading {evidences.Count} evidence items");

            ParallelOptions po = new() { MaxDegreeOfParallelism = 30 };

            DateTime downloadStart = DateTime.Now;
            Directory.CreateDirectory(directory);
            System.Collections.Concurrent.ConcurrentBag<Evidence> tempBag = new();
            Parallel.ForEach(evidences, po, e =>
            {
                e.DownloadFiles(directory);
                tempBag.Add(e);
                Console.WriteLine($"{tempBag.Count} / {evidences.Count} -> {(tempBag.Count * 100.0) / evidences.Count}%");
            });
            TimeSpan downloadTime = DateTime.Now.Subtract(downloadStart);
            string[] files = Directory.GetFiles(directory);
            long bytes = 0L;
            foreach (string f in files)
            {
                bytes += new FileInfo(f).Length;
            }
            Console.WriteLine($"Seconds: {downloadTime.TotalSeconds}");
            Console.WriteLine($"Bytes: {bytes}");
        }

        public void DownloadAllEvidence(string directory = @"C:\temp\files")
        {
            DownloadEvidence(Evidences, directory);
        }
    }

    public enum CaseStatus
    {
        any, open, plea_bargained, dismissed, tried_won, tried_lost, deleted
    }
}
