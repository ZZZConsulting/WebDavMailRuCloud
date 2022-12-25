namespace YaR.Clouds.Base.Repos.MailRuCloud
{
    public static class ConstSettings
    {
        public const string UserAgent =
            //"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/78.0.3904.108 Safari/537.36";
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36";

        //1public const string CloudDomain = "https://cloud.mail.ru";
        public const string MailCloudDomain = "https://cloud.mail.ru";
        public const string YandexCloudDomain = "https://cloud-api.yandex.net";

        //public const string PublishFileLink = CloudDomain + "/public/";

        //1public const string DefaultRequestType = "application/x-www-form-urlencoded";
        public const string MailDefaultRequestType = "application/x-www-form-urlencoded";
        public const string YandexDefaultRequestType = "application/json";
    }
}
