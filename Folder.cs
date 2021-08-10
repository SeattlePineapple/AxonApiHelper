using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AxonApiHelper
{
    public class Folder
    {
        public Guid ID;
        public string Name;
        public int EvidenceCount;
        public Case Case;

        internal static Folder FromJsonElement(JsonElement element)
        {
            Folder f = new();
            if (element.TryGetProperty("id", out JsonElement id))
            {
                f.ID = id.GetGuid();
            }
            if (element.TryGetProperty("attributes", out JsonElement attributes))
            {
                if (attributes.TryGetProperty("name", out JsonElement name))
                {
                    f.Name = name.GetString();
                }
            }
            if (element.TryGetProperty("meta", out JsonElement meta))
            {
                if (meta.TryGetProperty("evidenceCount", out JsonElement count))
                {
                    f.EvidenceCount = count.GetInt32();
                }
            }
            return f;
        }

        public void AddEvidence(Evidence e)
        {
            AddEvidence(new List<Evidence>() { e });
        }

        public void AddEvidence(List<Evidence> evidenceList)
        {
            List<string> evidenceJson = new();
            foreach (Evidence e in evidenceList)
            {
                evidenceJson.Add(@$"{{ ""type"": ""evidence"", ""id"": ""{e.ID}"" }}");
            }

            string content = @$"
            {{
                ""data"": [
                    { string.Join(',', evidenceJson) }
                ]
            }}";
            string response = Endpoint.Post($"cases/{Case.ID}/relationships/folders/{ID}/evidence", content, 2);
            using JsonDocument document = JsonDocument.Parse(response);
            JsonElement root = document.RootElement;
            JsonElement data = root.GetProperty("data");
            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement d in data.EnumerateArray())
                {
                    if (d.TryGetProperty("meta", out JsonElement meta))
                    {
                        if (meta.TryGetProperty("evidenceCount", out JsonElement evidenceCount))
                        {
                            if (evidenceCount.GetInt32() == EvidenceCount + 1)
                            {
                                EvidenceCount++;
                            }
                            else
                            {
                                throw new Exception("added more than 1?");
                            }
                        }
                    }
                }
            }
            else
            {
                if (data.TryGetProperty("meta", out JsonElement meta))
                {
                    if (meta.TryGetProperty("evidenceCount", out JsonElement evidenceCount))
                    {
                        if (evidenceCount.GetInt32() == EvidenceCount + 1)
                        {
                            EvidenceCount++;
                        }
                        else
                        {
                            throw new Exception("added more than 1?");
                        }
                    }
                }
            }
        }
    }
}
