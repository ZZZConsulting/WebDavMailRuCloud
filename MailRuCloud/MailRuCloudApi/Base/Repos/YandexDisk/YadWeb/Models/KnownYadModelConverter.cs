using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class KnownYadModelConverter : JsonConverter<List<YadResponseModel>>
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(KnownYadModelConverter));

    private readonly List<object> _createdModels;

    public KnownYadModelConverter(List<object> createdModels)
    {
        _createdModels = createdModels;
    }

    public override List<YadResponseModel> ReadJson(JsonReader reader, Type objectType,
        List<YadResponseModel> existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        var children = token.Children().ToList();
        for (int i = 0; i < children.Count; i++)
        {
            var chToken = children[i];
            var resItem = _createdModels[i];
            try
            {
                serializer.Populate(chToken.CreateReader(), resItem);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error unpacking JSON: {ex.Message}");
            }
        }

        return null;
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, List<YadResponseModel> value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
