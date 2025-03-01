using System;
using System.Collections.Generic;
using System.Threading;
using YaR.Clouds.Base;

namespace YaR.Clouds.Extensions;

public static class Extensions
{
    internal static IEnumerable<PublicLinkInfo> ToPublicLinkInfos(this string uriString, string publicBaseUrl)
    {
        if (!string.IsNullOrEmpty(uriString))
            yield return new PublicLinkInfo("", publicBaseUrl, uriString);
    }


    internal static DateTime ToDateTime(this ulong unixTimeStamp)
    {
        var dtDateTime = Epoch.AddSeconds(unixTimeStamp);
        return dtDateTime;
    }

    internal static long ToUnix(this DateTime date)
    {
        TimeSpan diff = date.ToUniversalTime() - Epoch;

        long seconds = diff.Ticks / TimeSpan.TicksPerSecond;
        return seconds;
    }

    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

    internal static byte[] HexStringToByteArray(this string hex)
    {
        int len = hex.Length;
        byte[] bytes = new byte[len / 2];
        for (int i = 0; i < len; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }

    public static string ToHexString(this byte[] ba)
    {
        string hex = BitConverter.ToString(ba);
        //return hex;
        return hex.Replace("-", "");
    }

    //public static string ReadAsText(this WebResponse resp, CancellationTokenSource cancelToken)
    //{
    //    using (var stream = new MemoryStream())
    //    {
    //        try
    //        {
    //            resp.ReadAsByte(cancelToken.Token, stream);
    //            return Encoding.UTF8.GetString(stream.ToArray());
    //        }
    //        catch
    //        {
    //            //// Cancellation token.
    //            return "7035ba55-7d63-4349-9f73-c454529d4b2e";
    //        }
    //    }
    //}

    //public static void ReadAsByte(this WebResponse resp, CancellationToken token, Stream outputStream = null)
    //{
    //    using Stream responseStream = resp.GetResponseStream();
    //    var buffer = new byte[65536];
    //    int bytesRead;

    //    while (responseStream != null && (bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
    //    {
    //        token.ThrowIfCancellationRequested();
    //        outputStream?.Write(buffer, 0, bytesRead);
    //    }
    //}

    public static T ThrowIf<T>(this T data, Func<T, bool> func, Func<T, Exception> ex)
    {
        if (func(data)) throw ex(data);
        return data;
    }

    //public static T ThrowIf<T>(this Task<T> data, Func<T, bool> func, Exception ex)
    //{
    //    var res = data.Result;
    //    if (func(res)) throw ex;
    //    return res;
    //}


    /// <summary>
    /// Finds the first exception of the requested type.
    /// </summary>
    /// <typeparam name="T">
    /// The type of exception to return
    /// </typeparam>
    /// <param name="ex">
    /// The exception to look in.
    /// </param>
    /// <returns>
    /// The exception or the first inner exception that matches the
    /// given type; null if not found.
    /// </returns>
    public static T InnerOf<T>(this Exception ex)
        where T : Exception
    {
        return (T)InnerOf(ex, typeof(T));
    }

    /// <summary>
    /// Finds the first exception of the requested type.
    /// </summary>
    /// <param name="ex">
    /// The exception to look in.
    /// </param>
    /// <param name="t">
    /// The type of exception to return
    /// </param>
    /// <returns>
    /// The exception or the first inner exception that matches the
    /// given type; null if not found.
    /// </returns>
    public static Exception InnerOf(this Exception ex, Type t)
    {
        while (true)
        {
            if (ex == null || t.IsInstanceOfType(ex)) return ex;

            if (ex is AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    var ret = InnerOf(e, t);
                    if (ret != null) return ret;
                }
            }

            ex = ex.InnerException;
        }
    }

    public static bool ContainsIgnoreCase(this string stringSearchIn, string stringToSearchFor,
        StringComparison comparisonType = StringComparison.InvariantCultureIgnoreCase)
    {
#if NET48
        System.Globalization.CultureInfo cu = comparisonType == StringComparison.InvariantCultureIgnoreCase
            ? System.Globalization.CultureInfo.InvariantCulture
            : System.Globalization.CultureInfo.CurrentCulture;
        return cu.CompareInfo.IndexOf(stringSearchIn, stringToSearchFor) >= 0;
#else
        return stringSearchIn.Contains(stringToSearchFor, comparisonType);
#endif
    }

    /// <summary>Finds all exception of the requested type.</summary>
    /// <typeparam name="T">The type of exception to look for.</typeparam>
    /// <param name="exception">The exception to look in.</param>
    /// <returns>
    /// <para>
    /// The enumeration of exceptions matching the specified type or null if not found.
    /// </para>
    /// <para>
    /// </returns>
    public static IEnumerable<T> OfType<T>(this Exception exception) where T : Exception
    {
        return exception.OfType<T>(false);
    }

    /// <summary>Finds all exception of the requested type.</summary>
    /// <typeparam name="T">The type of exception to look for.</typeparam>
    /// <param name="exception">The exception to look in.</param>
    /// <returns>
    /// <para>
    /// If throwIfNotNotFound=false,
    /// the enumeration of exceptions matching the specified type or null if not found.
    /// </para>
    /// <para>
    /// If throwIfNotNotFound=true,
    /// the enumeration of exceptions matching the specified type or throws the flatten AggregateException if not found.
    /// </para>
    /// </returns>
    public static IEnumerable<T> OfType<T>(this Exception exception, bool throwIfNotNotFound = false) where T : Exception
    {
        bool found = false;
        if (exception is not null)
        {
            if (exception is T ex1)
            {
                found = true;
                yield return ex1;
            }

            if (exception is AggregateException ae)
            {
                for (int index = 0; index < ae.InnerExceptions.Count; index++)
                {
                    Exception innerEx = ae.InnerExceptions[index];
                    if (innerEx is T ex2)
                    {
                        found = true;
                        yield return ex2;
                    }
                    if (innerEx.InnerException is not null)
                    {
                        foreach (var item in innerEx.OfType<T>(throwIfNotNotFound))
                        {
                            found = true;
                            yield return item;
                        }
                        ;
                    }
                }
            }
            else
            if (exception.InnerException.InnerException is not null)
            {
                foreach (var item in exception.InnerException.OfType<T>(throwIfNotNotFound))
                {
                    found = true;
                    yield return item;
                }
                ;
            }
        }

        if (!found && throwIfNotNotFound)
            throw exception;
    }

    /// <summary>Finds first occurrence of exception of the requested type.</summary>
    /// <typeparam name="T">The type of exception to look for.</typeparam>
    /// <param name="exception">The exception to look in.</param>
    /// <returns>
    /// <para>
    /// The exception matching the specified type or null if not found.
    /// </para>
    /// </returns>
    public static T FirstOfType<T>(this Exception exception) where T : Exception
    {
        return exception.FirstOfType<T>(false);
    }

    /// <summary>Finds first occurrence of exception of the requested type.</summary>
    /// <typeparam name="T">The type of exception to look for.</typeparam>
    /// <param name="exception">The exception to look in.</param>
    /// <returns>
    /// <para>
    /// If throwIfNotNotFound=false,
    /// the exception matching the specified type or null if not found.
    /// </para>
    /// <para>
    /// If throwIfNotNotFound=true,
    /// the exceptions matching the specified type or throws the flatten AggregateException if not found.
    /// </para>
    /// </returns>
    public static T FirstOfType<T>(this Exception exception, bool throwIfNotNotFound = false) where T : Exception
    {
        if (exception is null)
            return null;

        if (exception is T ex1)
            return ex1;

        if (exception is AggregateException ae)
        {
            for (int index = 0; index < ae.InnerExceptions.Count; index++)
            {
                Exception innerEx = ae.InnerExceptions[index];
                if (innerEx.FirstOfType<T>() is T ex2)
                    return ex2;
            }
        }
        else
        if (exception.InnerException.FirstOfType<T>() is T ex3)
            return ex3;

        if (throwIfNotNotFound)
            throw exception;

        return null;
    }

    /// <summary>Searches through all inner exceptions for exception of the requested type and returns true if found.</summary>
    /// <typeparam name="T">The type of exception to look for.</typeparam>
    /// <param name="exception">The exception to look in.</param>
    /// <returns>
    /// <para>
    /// True if the exception of the requested type is found, otherwise False.
    /// </para>
    /// </returns>
    public static bool Contains<T>(this Exception exception) where T : Exception
    {
        if (exception is null)
            return false;

        if (exception is T)
            return true;

        if (exception is AggregateException ae)
        {
            for (int index = 0; index < ae.InnerExceptions.Count; index++)
            {
                Exception innerEx = ae.InnerExceptions[index];
                if (innerEx.Contains<T>())
                    return true;
            }
        }
        else
        if (exception.InnerException.Contains<T>())
            return true;

        return false;
    }

    public static void LockedAction(this SemaphoreSlim semaphore, Action action)
    {
        if (action is null)
            return;

        semaphore.Wait();
        try
        {
            action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async void LockedActionAsync(this SemaphoreSlim semaphore, Action action)
    {
        if (action is null)
            return;

        await semaphore.WaitAsync();
        try
        {
            action();
        }
        finally
        {
            semaphore.Release();
        }
    }
}
