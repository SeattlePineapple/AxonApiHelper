using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AxonApiHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            // This is some example functionality to download evidence by tag value.
            // Solution must be run as admin since Endpoint.cs reads and writes bearer tokens to the registry. That portion could be swapped out to save to flat file, for example


            // find case named args[0]
            List<Case> cases = Case.Search(args[0], CaseStatus.open).Where(c => c.Title.Equals(args[0])).ToList();

            // download evidence for case
            cases[0].GetEvidence();

            // filter items with specific tag args[1]
            List<Evidence> currentBatch = cases[0].Evidences.Where(e => e.Tags.Contains(args[1])).ToList();

            // download items with that tag to args[2]
            Case.DownloadEvidence(currentBatch, args[2]);

            // output a report of those files to args[2]
            EvidenceFileReport(currentBatch, Path.Combine(args[2], "AllEvidence.csv"));

            Console.WriteLine("DONE!");
        }


        private static void EvidenceFileReport(List<Evidence> evidence, string fileName)
        {
            Console.WriteLine("Creating Evidence File Dump");
            List<string> csvRows = new();
            csvRows.Add("\"EvidenceID\",\"Title\",\"Description\",\"Duration\",\"ExternalIDs\",\"ModifiedOn\",\"Notes\",\"RecordedOn\",\"Size\",\"Status\",\"UploadedOn\",\"Tags\",\"FileID\",\"ContentType\",\"ChecksumAlg\",\"AxonChecksum\",\"SeattleChecksum\",\"FileName\",\"FileType\",\"StorageTier\",\"LocalLocation\",\"FileSize\"");
            evidence.ForEach(e =>
            {
                e.Files.ForEach(f =>
                {
                    csvRows.Add($"\"{e.ID}\",\"{e.Title.Replace("\"", "'")}\",\"{e.Description?.Replace("\"", "'") ?? string.Empty}\",\"{e.Duration}\",\"{string.Join("|", e.ExternalIDs)}\",\"{e.ModifiedOn}\",\"{string.Join("|", e.Notes ?? new List<string>(0)).Replace("\"", "'")}\",\"{e.RecordedOn}\",\"{e.Size}\",\"{e.Status}\",\"{e.UploadedOn}\",\"{(e.Tags != null ? string.Join('|', e.Tags) : string.Empty)}\",\"{f.ID}\",\"{f.ContentType}\",\"{f.ChecksumAlg}\",\"{f.Checksum}\",\"{f.LawChecksum}\",\"{f.FileName}\",\"{f.FileType}\",\"{f.StorageTier}\",\"{f.LocalLocation}\",\"{new FileInfo(f.LocalLocation).Length}\"");
                });
            });
            File.WriteAllLines(fileName, csvRows);
        }
    }
}
