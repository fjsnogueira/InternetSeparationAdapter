﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Google.Apis.Http;
using Nito.AsyncEx;

namespace InternetSeparationAdapter
{
  internal class Program
  {
    private static readonly string[] Scopes = {GmailService.Scope.GmailReadonly};
    private const string ApplicationName = "Internet Separation Adapter";


    public static int Main(string[] args)
    {
      var arguments = Parser.Default.ParseArguments<Arguments>(args)
        .MapResult(parsed => parsed,
          errors =>
          {
            foreach (var error in errors)
            {
              Console.WriteLine(error);
            }
            return null;
          }
      );

      return arguments == null ? 1 : AsyncContext.Run(() => AsyncMain(arguments));
    }

    private static async Task<int> AsyncMain(Arguments arguments)
    {
      var exitToken = new CancellationTokenSource();
      Console.CancelKeyPress += (sender, cancelArgs) =>
      {
        exitToken.Cancel();
      };

      var service = new Gmail(arguments.SecretsFile, arguments.CredentialsPath, Scopes,
        ApplicationName, exitToken.Token);
      var bot = new Broadcaster(0); // TODO: move this to a config file

      if (exitToken.IsCancellationRequested) return 1;

      var messages = await service.GetUnreadMessage(arguments.Label);

      foreach (var messageLazy in service.FetchMessages(messages))
      {
        var message = await messageLazy;
        if (exitToken.IsCancellationRequested) return 1;
        Console.WriteLine($"ID: {message.Id}");
        Console.WriteLine($"From: {message.From}");
        Console.WriteLine($"Subject: {message.Subject}");
        try
        {
          var body = message.Body;
          if (body.HasValue)
          {
            Console.WriteLine($"Body: {body.Value.Value}");
          }
          bot.SendToTelegram(message.FormattedMessage);
        }
        catch (NotImplementedException)
        {
          Console.WriteLine("Body: mutlipart/related");
        }
      }

      return 0;
    }

    public class Arguments
    {
      [Option('s', "secret", Required = true, HelpText = "Path to the Gmail API Secrets JSON file")]
      public string SecretsFile { get; set; }

      [Option('c', "credentials-path", HelpText = "Path to directory to store OAuth Credentials")]
      public string CredentialsPath { get; set; }

      [Option('l', "label", HelpText = "The default label to look for emails. Defaults to INBOX",
         Default = DefaultLabel)]
      public string Label { get; set; }

      public static string DefaultCredentialsPath
      {
        get
        {
          var credPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
          return Path.Combine(credPath, ".credentials/internet-separation-adapter.json");
        }
      }

      public const string DefaultLabel = "INBOX";
    }
  }
}
