using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests
{
    internal class YadAuthTotpRequest : BaseRequestJson<YadAuthTotpRequestResult>
    {
        private readonly string _csrf;
        private readonly string _trackId;
        private readonly string _totpSecret;

        public YadAuthTotpRequest(HttpCommonSettings settings, IAuth auth, string csrf, string trackId, string totpSecret)
            : base(settings, auth)
        {
            _csrf = csrf;
            _trackId = trackId;
            _totpSecret = totpSecret;
        }

        protected override string RelationalUri => "/registration-validations/confirm-rfc-totp";

        protected override HttpWebRequest CreateRequest(string baseDomain = null)
        {
            var request = base.CreateRequest("https://passport.yandex.ru");

            request.Accept = "application/json, text/javascript, */*; q=0.01";
            request.Referer = "https://passport.yandex.ru/";
            request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";

            request.Headers.Add("sec-ch-ua", "\" Not A; Brand\";v=\"99\", \"Chromium\";v=\"99\", \"Google Chrome\";v=\"99\"");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("Origin", "https://passport.yandex.ru");
            request.Headers.Add("Sec-Fetch-Site", "same-origin");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7,es;q=0.6");

            return request;
        }

        protected override byte[] CreateHttpContent()
        {
            var bytes = Base32Encoding.ToBytes(_totpSecret);

            var totp = new Totp(bytes);

            var remainingTime = totp.RemainingSeconds();
            if (remainingTime <= 3)
            {
                Thread.Sleep(TimeSpan.FromSeconds(4));
            }
            var result = totp.ComputeTotp();


            var keyValues = new List<KeyValuePair<string, string>>
            {
                new("csrf_token", _csrf),
                new("track_id", _trackId),
                new("otp", result)
            };
            FormUrlEncodedContent z = new FormUrlEncodedContent(keyValues);
            var d = z.ReadAsByteArrayAsync().Result;
            return d;
        }

        protected override RequestResponse<YadAuthTotpRequestResult> DeserializeMessage(NameValueCollection responseHeaders, Stream stream)
        {
            var res = base.DeserializeMessage(responseHeaders, stream);

            var uid = responseHeaders["X-Default-UID"];
            if (string.IsNullOrWhiteSpace(uid))
                throw new AuthenticationException("TOTP: Cannot get X-Default-UID");
            res.Result.DefaultUid = uid;
            return res;
        }
    }

    internal class YadAuthTotpRequestResult
    {
        public bool HasError => Status == "error";

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("errors")]
        public List<string> Errors { get; set; }

        [JsonIgnore]
        public string DefaultUid { get; set; }
    }

    public class Totp
    {
        private const long unixEpochTicks = 621355968000000000L;

        private const long ticksToSeconds = 10000000L;

        private const int step = 30;

        private const int totpSize = 6;

        private byte[] key;

        public Totp(byte[] secretKey)
        {
            key = secretKey;
        }

        public string ComputeTotp()
        {
            var window = CalculateTimeStepFromTimestamp(DateTime.UtcNow);

            var data = GetBigEndianBytes(window);

            var hmac = new HMACSHA1();
            hmac.Key = key;
            var hmacComputedHash = hmac.ComputeHash(data);

            int offset = hmacComputedHash[hmacComputedHash.Length - 1] & 0x0F;
            var otp = (hmacComputedHash[offset] & 0x7f) << 24
                   | (hmacComputedHash[offset + 1] & 0xff) << 16
                   | (hmacComputedHash[offset + 2] & 0xff) << 8
                   | (hmacComputedHash[offset + 3] & 0xff) % 1000000;

            var result = Digits(otp, totpSize);

            return result;
        }

        public int RemainingSeconds()
        {
            return step - (int)(((DateTime.UtcNow.Ticks - unixEpochTicks) / ticksToSeconds) % step);
        }

        private byte[] GetBigEndianBytes(long input)
        {
            // Since .net uses little endian numbers, we need to reverse the byte order to get big endian.
            var data = BitConverter.GetBytes(input);
            Array.Reverse(data);
            return data;
        }

        private long CalculateTimeStepFromTimestamp(DateTime timestamp)
        {
            var unixTimestamp = (timestamp.Ticks - unixEpochTicks) / ticksToSeconds;
            var window = unixTimestamp / (long)step;
            return window;
        }

        private string Digits(long input, int digitCount)
        {
            var truncatedValue = ((int)input % (int)Math.Pow(10, digitCount));
            return truncatedValue.ToString().PadLeft(digitCount, '0');
        }
    }


    public static class Base32Encoding
    {
        public static byte[] ToBytes(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException("input");
            }

            input = input.TrimEnd('='); //remove padding characters
            int byteCount = input.Length * 5 / 8; //this must be TRUNCATED
            byte[] returnArray = new byte[byteCount];

            byte curByte = 0, bitsRemaining = 8;
            int mask = 0, arrayIndex = 0;

            foreach (char c in input)
            {
                int cValue = CharToValue(c);

                if (bitsRemaining > 5)
                {
                    mask = cValue << (bitsRemaining - 5);
                    curByte = (byte)(curByte | mask);
                    bitsRemaining -= 5;
                }
                else
                {
                    mask = cValue >> (5 - bitsRemaining);
                    curByte = (byte)(curByte | mask);
                    returnArray[arrayIndex++] = curByte;
                    curByte = (byte)(cValue << (3 + bitsRemaining));
                    bitsRemaining += 3;
                }
            }

            //if we didn't end with a full byte
            if (arrayIndex != byteCount)
            {
                returnArray[arrayIndex] = curByte;
            }

            return returnArray;
        }

        public static string ToString(byte[] input)
        {
            if (input == null || input.Length == 0)
            {
                throw new ArgumentNullException("input");
            }

            int charCount = (int)Math.Ceiling(input.Length / 5d) * 8;
            char[] returnArray = new char[charCount];

            byte nextChar = 0, bitsRemaining = 5;
            int arrayIndex = 0;

            foreach (byte b in input)
            {
                nextChar = (byte)(nextChar | (b >> (8 - bitsRemaining)));
                returnArray[arrayIndex++] = ValueToChar(nextChar);

                if (bitsRemaining < 4)
                {
                    nextChar = (byte)((b >> (3 - bitsRemaining)) & 31);
                    returnArray[arrayIndex++] = ValueToChar(nextChar);
                    bitsRemaining += 5;
                }

                bitsRemaining -= 3;
                nextChar = (byte)((b << bitsRemaining) & 31);
            }

            //if we didn't end with a full char
            if (arrayIndex != charCount)
            {
                returnArray[arrayIndex++] = ValueToChar(nextChar);
                while (arrayIndex != charCount) returnArray[arrayIndex++] = '='; //padding
            }

            return new string(returnArray);
        }

        private static int CharToValue(char c)
        {
            int value = (int)c;

            //65-90 == uppercase letters
            if (value < 91 && value > 64)
            {
                return value - 65;
            }
            //50-55 == numbers 2-7
            if (value < 56 && value > 49)
            {
                return value - 24;
            }
            //97-122 == lowercase letters
            if (value < 123 && value > 96)
            {
                return value - 97;
            }

            throw new ArgumentException("Character is not a Base32 character.", "c");
        }

        private static char ValueToChar(byte b)
        {
            if (b < 26)
            {
                return (char)(b + 65);
            }

            if (b < 32)
            {
                return (char)(b + 24);
            }

            throw new ArgumentException("Byte is not a value Base32 value.", "b");
        }
    }
}
