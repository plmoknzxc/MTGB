using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>Field-only snapshot parsing/copying for both builds. Avoid Unity's recursive DTO depth scan.</summary>
internal static class ServerSnapshotJson
{
    // Validate the sender's actual field set before projecting into this build's
    // DTO. Otherwise newly added optional fields change old-server checksums.
    private sealed class PlayerViewReader : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanConvert(Type type) => type == typeof(OnlinePlayerViewState);
        public override object ReadJson(JsonReader reader,Type type,object existing,JsonSerializer serializer)
        {
            if(reader.TokenType==JsonToken.Null)return null;
            var fields=JObject.Load(reader);
            var view=new OnlinePlayerViewState();
            using(var source=fields.CreateReader())serializer.Populate(source,view);
            view.receivedWireHash=HashViewFields(fields);
            view.receivedTypedHash=HashView(view);
            return view;
        }
        public override void WriteJson(JsonWriter writer,object value,JsonSerializer serializer)
            => throw new NotSupportedException();
    }
    private sealed class SnapshotFields : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = new List<JsonProperty>();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.IsNotSerialized) continue;
                JsonProperty property = CreateProperty(field, MemberSerialization.Fields);
                property.Readable = true;
                property.Writable = !field.IsInitOnly;
                properties.Add(property);
            }
            return properties;
        }
    }

    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        ContractResolver = new SnapshotFields(),
        TypeNameHandling = TypeNameHandling.None,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        MaxDepth = 64,
        Converters = new List<JsonConverter>{new PlayerViewReader()}
    };

    public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);
    public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    internal static void WriteTo(System.IO.TextWriter target,object value)
    {
        using var writer=new JsonTextWriter(target){CloseOutput=false};
        JsonSerializer.Create(Settings).Serialize(writer,value);writer.Flush();
    }
    internal static T ReadFrom<T>(System.IO.TextReader source)
    {
        using var reader=new JsonTextReader(source){CloseInput=false,MaxDepth=64};
        var serializer=JsonSerializer.Create(Settings);serializer.CheckAdditionalContent=true;
        return serializer.Deserialize<T>(reader);
    }
    public static T Copy<T>(T value) => Deserialize<T>(Serialize(value));
    internal static string HashView(OnlinePlayerViewState view)
        => HashViewFields(JObject.FromObject(view,JsonSerializer.Create(Settings)));
    private static string HashViewFields(JObject fields)
    {
        fields["viewHash"]=string.Empty;
        using var sha=SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(fields.ToString(Formatting.None)))).Replace("-",string.Empty);
    }
}
