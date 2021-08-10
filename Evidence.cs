using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AxonApiHelper
{
    public class Evidence
    {
        public Guid ID;
        public string Status;
        public string Title
        {
            get; private set;
        }
        public string Description;
        public List<string> ExternalIDs;
        public DateTime RecordedOn;
        public DateTime UploadedOn;
        public DateTime ModifiedOn;
        public User UploadedBy;
        public decimal? Duration;
        public long Size;
        public List<string> Notes;
        public List<Case> Cases;
        public List<string> Tags;
        public List<EvidenceFile> Files;

        public Evidence(Guid id)
        {
            ID = id;
            Cases = new List<Case>();
        }

        // If Evidence created as reference from another query, where it just has an ID, this will get other properties
        public void Enumerate()
        {
            Evidence copy = Parse(Endpoint.Get($"evidence/{ID}"), out _)[0];
            Status = copy.Status;
            Title = copy.Title;
            Description = copy.Description;
            ExternalIDs = copy.ExternalIDs;
            RecordedOn = copy.RecordedOn;
            UploadedOn = copy.UploadedOn;
            ModifiedOn = copy.ModifiedOn;
            UploadedBy = copy.UploadedBy;
            Duration = copy.Duration;
            Size = copy.Size;
            Notes = copy.Notes;
            Cases = copy.Cases;
            Tags = copy.Tags;
            Files = copy.Files;
        }

        public static List<Evidence> Search(string titleContains = null)
        {
            string titleParam = string.IsNullOrEmpty(titleContains) ? string.Empty : $"title={titleContains}";
            string limitParam = "limit=500";
            string offsetParam = "offset=0";
            List<string> paramList = new() { titleParam, limitParam, offsetParam };
            paramList.RemoveAll((a) => { return a.Equals(string.Empty); });
            List<Evidence> evidences = Parse(Endpoint.Get($"evidence{(paramList.Count == 0 ? string.Empty : "?" + string.Join('&', paramList))}"), out int count);

            while (evidences.Count < count)
            {
                offsetParam = $"offset={evidences.Count}";
                paramList = new List<string>() { titleParam, limitParam, offsetParam };
                paramList.RemoveAll((a) => { return a.Equals(string.Empty); });
                evidences.AddRange(Parse(Endpoint.Get($"evidence{(paramList.Count == 0 ? string.Empty : "?" + string.Join('&', paramList))}"), out _));
            }
            Logger.Log($"Evidence search for query \"{titleContains ?? "*any*"}\" returned {evidences.Count} results");
            return evidences;
        }

        public static List<Evidence> FromCase(Guid ID)
        {
            string limitParam = "limit=500";
            string offsetParam = "offset=0";
            List<string> paramList = new() { limitParam, offsetParam };
            List<Evidence> evidences = Parse(Endpoint.Get($"cases/{ID}/relationships/evidence{(paramList.Count == 0 ? string.Empty : "?" + string.Join('&', paramList))}"), out int count);
            while (evidences.Count < count)
            {
                offsetParam = $"offset={evidences.Count}";
                paramList = new List<string>() { limitParam, offsetParam };
                paramList.RemoveAll((a) => { return a.Equals(string.Empty); });
                evidences.AddRange(Parse(Endpoint.Get($"cases/{ID}/relationships/evidence{(paramList.Count == 0 ? string.Empty : "?" + string.Join('&', paramList))}"), out _));
            }
            ParallelOptions po = new() { MaxDegreeOfParallelism = 30 };
            Parallel.For(0, evidences.Count, po, (i) => { evidences[i].Enumerate(); });
            return evidences;
        }

        private static List<Evidence> Parse(string json, out int count)
        {
            List<Evidence> evidences = new();
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            count = root.TryGetProperty("meta", out JsonElement meta) ? meta.GetProperty("count").GetInt32() : 1;

            JsonElement data = root.GetProperty("data");
            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in data.EnumerateArray())
                {
                    evidences.Add(FromJsonElement(element));
                }
            }
            else
            {
                evidences.Add(FromJsonElement(data));
            }

            return evidences;
        }

        private static Evidence FromJsonElement(JsonElement element)
        {
            Evidence e = new(element.TryGetProperty("id", typeof(Guid)));
            if (element.TryGetProperty("attributes", out JsonElement attributes))
            {
                e.Status = attributes.TryGetProperty("status", typeof(string));
                e.Title = attributes.TryGetProperty("title", typeof(string));
                e.Description = attributes.TryGetProperty("description", typeof(string));
                e.ExternalIDs = new List<string>(attributes.TryGetProperty("externalId", typeof(string))?.Split(',', StringSplitOptions.RemoveEmptyEntries)).Select(s => s.Trim()).ToList();
                e.RecordedOn = attributes.TryGetProperty("recordedOn", typeof(DateTime));
                e.UploadedOn = attributes.TryGetProperty("uploadedOn", typeof(DateTime));
                e.ModifiedOn = attributes.TryGetProperty("modifiedOn", typeof(DateTime));
                e.Duration = attributes.TryGetProperty("duration", typeof(decimal));
                e.Size = attributes.TryGetProperty("size", typeof(long));
                List<JsonElement> noteElements = attributes.TryGetProperty("notes", typeof(List<JsonElement>));
                if (noteElements?.Count > 0)
                {
                    e.Notes = new List<string>(0);
                    noteElements.ForEach((je) => e.Notes.Add(je.GetString()));
                }
                List<JsonElement> tagElements = attributes.TryGetProperty("tags", typeof(List<JsonElement>));
                if (tagElements?.Count > 0)
                {
                    e.Tags = new List<string>(0);
                    tagElements.ForEach((je) => e.Tags.Add(je.GetString()));
                }
                JsonElement caseIDs = attributes.GetProperty("evidenceCaseIds");
                if (!caseIDs.GetRawText().Equals("null"))
                {
                    foreach (JsonElement j in caseIDs.EnumerateArray())
                    {
                        Case c = new(Guid.Parse(j.GetString()));
                        e.Cases.Add(c);
                    }
                }
            }
            if (element.TryGetProperty("relationships", out JsonElement relationships))
            {
                if (relationships.TryGetProperty("uploadedBy", out JsonElement uploader))
                {
                    e.UploadedBy = User.FromJsonElement(uploader);
                }

                if (relationships.TryGetProperty("files", out JsonElement files))
                {
                    if (files.TryGetProperty("data", out JsonElement data))
                    {
                        e.Files = new();
                        foreach (JsonElement df in data.EnumerateArray())
                        {
                            e.Files.Add(EvidenceFile.FromJsonElement(df, ref e));
                        }
                    }
                }
            }

            return e;
        }

        /// <summary>
        /// Updates Title, Description
        /// </summary>
        public void SetTitle(string newTitle)
        {
            if (!Title.Equals(newTitle))
            {
                string content = @$"
                {{
                    ""data"": {{
                        ""title"": ""{newTitle}""
                    }}
                }}";
                if (Endpoint.Patch($"evidence/{ID}/title", content).Contains(newTitle))
                {
                    Title = newTitle;
                }
                else
                {
                    //failure
                }
            }
        }


        /// <summary>
        /// Removes tag by tag content
        /// </summary>
        /// <param name="tagValue">Tag content</param>
        /// <returns>Successful</returns>
        public void RemoveTag(string tagValue)
        {
            string content = @$"
            {{
                ""data"": [
                    {{ ""type"": ""tag"", ""id"": ""{tagValue}"" }}
                ]
            }}";
            if (!Endpoint.Delete($"evidence/{ID}/tags", content).Contains(tagValue))
            {
                Tags.Remove(tagValue);
            }
            else
            {
                throw new Exception("Failed to remove tag");
            }
        }

        public void AddTag(string tagValue)
        {
            string content = @$"
            {{
                ""data"": [
                    {{ ""type"": ""tag"", ""id"": ""{tagValue}"" }}
                ]
            }}";
            if (Endpoint.Post($"evidence/{ID}/tags", content).Contains(tagValue))
            {
                if (Tags == null) Tags = new List<string>();
                Tags.Add(tagValue);
            }
            else
            {
                throw new Exception("Failed to add tag");
            }
        }

        public void DownloadFiles(string directory)
        {
            if (Files != null)
            {
                foreach (EvidenceFile f in Files)
                {
                    f.Download(directory);
                }
            }
        }

        public string GetSecureLink()
        {
            using JsonDocument doc = JsonDocument.Parse(Endpoint.Get($"evidence/{ID}/secure-link"));
            JsonElement data = doc.RootElement.GetProperty("data").GetProperty("attributes");
            return data.GetProperty("link").GetString();
        }
    }
}
