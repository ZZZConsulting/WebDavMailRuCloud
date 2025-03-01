using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadPostData
{
    public string Sk { get; set; }
    public string IdClient { get; set; }
    public List<YadPostModel> Models { get; set; } = new();

    public byte[] CreateHttpContent()
    {
        var keyValues = new List<KeyValuePair<string, string>>
        {
            new("sk", Sk),
            new("idClient", IdClient)
        };

        keyValues.AddRange(Models.SelectMany((model, i) => model.ToKvp(i)));

        FormUrlEncodedContent z = new FormUrlEncodedContent(keyValues);
        return z.ReadAsByteArrayAsync().Result;
    }
}

internal abstract class YadPostModel
{
    public virtual IEnumerable<KeyValuePair<string, string>> ToKvp(int index)
    {
        yield return new KeyValuePair<string, string>($"_model.{index}", Name);
    }

    public string Name { get; set; }
}







public class YadResponseResult
{
    [JsonProperty("uid")]
    public long Uid { get; set; }

    [JsonProperty("login")]
    public string Login { get; set; }

    [JsonProperty("sk")]
    public string Sk { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("models")]
    public List<YadResponseModel> Models { get; set; }
}

public class YadResponseModel
{
    [JsonProperty("model")]
    public string ModelName { get; set; }

    [JsonProperty("error")]
    public YadResponseError Error { get; set; }
}


public class YadResponseModel<TData, TParams> : YadResponseModel
//where TData : YadModelDataBase
{
    [JsonProperty("params")]
    public TParams Params { get; set; }

    [JsonProperty("data")]
    public TData Data { get; set; }
}

public class YadResponseError
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}



public class YadModelDataBase
{
    [JsonProperty("error")]
    public YadModelDataError Error { get; set; }
}

public class YadModelDataBaseErrorStruct
{
    [JsonProperty("error")]
    public YadModelDataError2 Error { get; set; }
}

public class YadModelDataBaseErrorString
{
    [JsonProperty("error")]
    public string ErrorToken { get; set; }
}

public class YadModelDataError
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("body")]
    public YadModelDataErrorBody Body { get; set; }
}

public class YadModelDataErrorBody
{
    [JsonProperty("code")]
    public long Code { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }
}

public class YadModelDataError2
{
    [JsonProperty("id")]
    public string Id { get; set; }

    //[JsonProperty("body")]
    //public YadModelDataErrorBody Body { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("statusCode")]
    public int StatusCode { get; set; }

    [JsonProperty("code")]
    public long Code { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }
}
