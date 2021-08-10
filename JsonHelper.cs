using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AxonApiHelper
{
    public static class JsonHelper
    {
        public static dynamic TryGetProperty(this JsonElement parentElement, string propertyName, Type dataType)
        {
            if (parentElement.TryGetProperty(propertyName, out JsonElement propertyElement))
            {
                if (propertyElement.GetRawText().Equals("null"))
                    return null;


                return (dataType.Name) switch
                {
                    "String" => propertyElement.GetString(),
                    "List`1" => propertyElement.EnumerateArray().ToList(),
                    "bool" => propertyElement.GetBoolean(),
                    "DateTime" => propertyElement.GetDateTime(),
                    "Guid" => propertyElement.GetGuid(),
                    "Decimal" => propertyElement.GetDecimal(),
                    "Int32" => propertyElement.GetInt32(),
                    "Int64" => propertyElement.GetInt64(),
                    _ => throw new Exception("Unsupported Data Type"),
                };
            }
            else
            {
                return null;
            }
        }

        public static List<JsonElement> ToList(this JsonElement.ArrayEnumerator e)
        {
            List<JsonElement> elements = new(0);
            while (e.MoveNext())
            {
                elements.Add(e.Current);
            }
            return elements;
        }

        public static List<T> Parse<T>(string json, out int count)
        {
            List<T> list = new();
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            count = root.TryGetProperty("meta", out JsonElement meta) ? meta.GetProperty("count").GetInt32() : 1;

            JsonElement data = root.GetProperty("data");
            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in data.EnumerateArray())
                {
                    T item = (T)typeof(T).GetMethod("FromJsonElement").Invoke(typeof(T).GetConstructor(new Type[] { typeof(JsonElement) }), new object[] { element });
                    list.Add(item);
                }
            }
            else
            {
                T item = (T)typeof(T).GetMethod("FromJsonElement").Invoke(typeof(T).GetConstructor(new Type[] { typeof(JsonElement) }), new object[] { data });
                list.Add(item);
            }

            return list;
        }
    }
}