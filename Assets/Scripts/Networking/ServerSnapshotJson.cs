using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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
            var view=new OnlinePlayerViewState();
            using(var sha=SHA256.Create())
            using(var crypto=new CryptoStream(System.IO.Stream.Null,sha,CryptoStreamMode.Write))
            {
                using(var text=new System.IO.StreamWriter(crypto,new UTF8Encoding(false),1024,true))
                using(var writer=new JsonTextWriter(text){CloseOutput=false})
                using(var source=new ViewHashReader(reader,writer))serializer.Populate(source,view);
                crypto.FlushFinalBlock();
                view.receivedWireHash=BitConverter.ToString(sha.Hash).Replace("-",string.Empty);
            }
            view.receivedTypedHash=HashView(view);
            return view;
        }
        public override void WriteJson(JsonWriter writer,object value,JsonSerializer serializer)
            => throw new NotSupportedException();
    }
    // Hash the sender's tokens as the serializer reads them, including unknown
    // fields. Keeping both wire and typed hashes still detects local mutation,
    // without retaining a second object graph or a canonical JSON string.
    private sealed class ViewHashReader : JsonReader
    {
        private readonly JsonReader source;
        private readonly JsonWriter target;
        private readonly int rootDepth;
        private bool emptyHash;
        public ViewHashReader(JsonReader source,JsonWriter target)
        {
            if(source.TokenType!=JsonToken.StartObject)throw new JsonSerializationException("Expected player view object.");
            this.source=source;this.target=target;rootDepth=source.Depth;
            Culture=source.Culture;DateParseHandling=source.DateParseHandling;
            DateTimeZoneHandling=source.DateTimeZoneHandling;FloatParseHandling=source.FloatParseHandling;
            MaxDepth=source.MaxDepth;CloseInput=false;
            CopyToken();
        }
        public override bool Read()
        {
            do {if(!source.Read()){SetToken(JsonToken.None);return false;}}
            while(source.TokenType==JsonToken.Comment);
            CopyToken();return true;
        }
        private void CopyToken()
        {
            SetToken(source.TokenType,source.Value);
            if(emptyHash)
            {
                if(source.TokenType!=JsonToken.String&&source.TokenType!=JsonToken.Null)
                    throw new JsonSerializationException("Expected player view hash string.");
                target.WriteValue(string.Empty);emptyHash=false;
            }
            else target.WriteToken(source.TokenType,source.Value);
            if(source.TokenType==JsonToken.PropertyName&&source.Depth==rootDepth+1
                &&string.Equals(source.Value as string,"viewHash",StringComparison.Ordinal))emptyHash=true;
        }
    }
    private sealed class SnapshotFields : DefaultContractResolver
    {
        private readonly bool hashing;
        public SnapshotFields(bool hashing=false){this.hashing=hashing;}
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = new List<JsonProperty>();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.IsNotSerialized) continue;
                JsonProperty property = CreateProperty(field, MemberSerialization.Fields);
                property.Readable = true;
                property.Writable = !field.IsInitOnly;
                if(hashing&&field.DeclaringType==typeof(OnlinePlayerViewState)&&field.Name=="viewHash")
                    property.ValueProvider=new EmptyHash();
                properties.Add(property);
            }
            return properties;
        }
    }
    private sealed class EmptyHash : IValueProvider
    {
        public object GetValue(object target)=>string.Empty;
        public void SetValue(object target,object value)=>throw new NotSupportedException();
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
    private static readonly JsonSerializerSettings HashSettings=new JsonSerializerSettings
    {
        ContractResolver=new SnapshotFields(true),TypeNameHandling=TypeNameHandling.None,
        ReferenceLoopHandling=ReferenceLoopHandling.Error,MaxDepth=64
    };
    private static readonly JsonSerializerSettings ReplaySettings=new JsonSerializerSettings
    {
        ContractResolver=Settings.ContractResolver,TypeNameHandling=TypeNameHandling.None,
        ReferenceLoopHandling=ReferenceLoopHandling.Error,MaxDepth=64,
        NullValueHandling=NullValueHandling.Ignore,DefaultValueHandling=DefaultValueHandling.Ignore
    };

    public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);
    internal static string SerializeReplayFrame(ReplayFrame frame)=>JsonConvert.SerializeObject(frame,ReplaySettings);
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
    {
        // The canonical field order/escaping is unchanged. Feed UTF-8 directly
        // to SHA instead of allocating a JToken tree, JSON string and byte array.
        using var sha=SHA256.Create();
        using var crypto=new CryptoStream(System.IO.Stream.Null,sha,CryptoStreamMode.Write);
        using(var text=new System.IO.StreamWriter(crypto,new UTF8Encoding(false),1024,true))
        using(var writer=new JsonTextWriter(text){CloseOutput=false})
            JsonSerializer.Create(HashSettings).Serialize(writer,view);
        crypto.FlushFinalBlock();
        return BitConverter.ToString(sha.Hash).Replace("-",string.Empty);
    }
}
