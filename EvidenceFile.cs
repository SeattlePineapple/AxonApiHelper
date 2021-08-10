using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AxonApiHelper
{
    public class EvidenceFile
    {
        public Guid ID;
        public string ContentType;
        public string ChecksumAlg;
        public string Checksum;
        public string LawChecksum;
        public string FileName;
        public string FileType;
        public string StorageTier;
        public Evidence Evidence;
        public string LocalLocation;

        public EvidenceFile(ref Evidence e)
        {
            Evidence = e;
        }

        public void Download(string directory)
        {
            string location = Path.Combine(directory, FileName);
            FileInfo fi = new(location);
            if (fi.Exists)
            {
                Console.WriteLine($"Exists {FileName}");
                LocalLocation = location;
                CalculateChecksum();
                return;
            }
            try
            {
                if (CSHelpers.Retry(() => Endpoint.DownloadBytes(location, $"evidence/{Evidence.ID}/files/{ID}"), true, 2))
                {
                    LocalLocation = location;
                    CalculateChecksum();
                    Console.WriteLine($"Completed {FileName}");
                }
                else
                {
                    Console.WriteLine($"Download failed for {FileName}");
                }
            }
            catch
            {
                Console.WriteLine($"Download failed for {FileName}");
            }
        }

        public static EvidenceFile FromJsonElement(JsonElement df, ref Evidence e)
        {
            EvidenceFile ef = new(ref e);
            ef.ID = df.TryGetProperty("id", typeof(Guid));
            if (df.TryGetProperty("attributes", out JsonElement dfa))
            {
                ef.ContentType = dfa.TryGetProperty("contentType", typeof(string));
                ef.ChecksumAlg = dfa.TryGetProperty("checksumAlg", typeof(string));
                ef.Checksum = dfa.TryGetProperty("checksum", typeof(string));
                ef.FileName = dfa.TryGetProperty("fileName", typeof(string));
                ef.FileType = dfa.TryGetProperty("fileType", typeof(string));
                ef.StorageTier = dfa.TryGetProperty("storageTier", typeof(string));
            }
            return ef;
        }

        // not all files currently have checksum to verify. only original videos do, not clips or redactions
        private void CalculateChecksum()
        {
            using SHA256 s = SHA256.Create();
            using FileStream fs = new FileInfo(LocalLocation).Open(FileMode.Open);
            byte[] hashValue = s.ComputeHash(fs);
            LawChecksum = HashToString(hashValue);
        }

        private static string HashToString(byte[] array)
        {
            StringBuilder sb = new();
            for (int i = 0; i < array.Length; i++)
            {
                sb.Append($"{array[i]:X2}");
            }
            return sb.ToString();
        }
    }
}
