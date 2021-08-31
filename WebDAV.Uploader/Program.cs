﻿using System;
using CommandLine;

namespace YaR.CloudMailRu.Client.Console
{
    static class Program
    {
        private static void Main(string[] args)
        {
            //System.Console.WriteLine($"{args[0]} {args[1]} {args[2]} {args[3]}");

            var exitCode = Parser.Default.ParseArguments<UploadOptions, DecryptOptions>(args)
                .MapResult(
                    (UploadOptions opts) => UploadStub.Upload(opts),
                    (DecryptOptions opts) => DecryptStub.Decrypt(opts),
                    errs => 1);

            if (exitCode > 0) Environment.Exit(exitCode);
            System.Console.ReadKey();
        }
    }
}
