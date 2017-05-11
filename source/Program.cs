using Microsoft.ProjectOxford.Text.Sentiment;
using System;
using System.Collections.Generic;
using Microsoft.ProjectOxford.Text.Core;
using Microsoft.ProjectOxford.Text.Language;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Text.KeyPhrase;
using Microsoft.ProjectOxford.ContentModerator;
using System.IO;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;

namespace Pipeline
{
    class Program
    {
        private static string inputStorageAccountConnection = "";
        private static string inputFileContainer = "";
        private static string inputFile = "";

        private static string outputStorageAccountConnection = "";
        private static string outputFileContainer = "";
        private static string outputFile = "";

        private static string contentModeratorApiKey = "";
        private static string textAnalyticsApiKey = "";

        private static List<Conversation> conversations;

        static void Main(string[] args)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Beginning Cognitive Services data pipeline ({0})", DateTime.Now.ToString());

                LoadRawConversations().Wait();

                RunLanguageIdentification().Wait();

                RunContentModeration().Wait();

                RunSentimentAnalysis().Wait();

                RunKeyPhraseDetection().Wait();

                SaveConversationsWithMetadata().Wait();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("Exception encountered ({0})", DateTime.Now.ToString());
                Console.WriteLine(ex.Message);
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        private static async Task LoadRawConversations()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Loading raw conversations from blob storage");

            var storageAccount = CloudStorageAccount.Parse(inputStorageAccountConnection);

            var blobClient = storageAccount.CreateCloudBlobClient();

            var container = blobClient.GetContainerReference(inputFileContainer);

            var blob = container.GetBlockBlobReference(inputFile);

            var json = "";

            using (var memoryStream = new MemoryStream())
            {
                await blob.DownloadToStreamAsync(memoryStream);
                json = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            }

            conversations = JsonConvert.DeserializeObject<List<Conversation>>(json);

            Console.WriteLine("Raw conversations loaded from blob storage");
            Console.WriteLine();
        }

        private static async Task SaveConversationsWithMetadata()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Saving conversations with metadata to {0}", outputFile);

            var json = JsonConvert.SerializeObject(conversations);

            var storageAccount = CloudStorageAccount.Parse(outputStorageAccountConnection);

            var blobClient = storageAccount.CreateCloudBlobClient();

            var container = blobClient.GetContainerReference(outputFileContainer);

            var blob = container.GetBlockBlobReference(outputFile);

            await blob.UploadTextAsync(json);

            Console.WriteLine("Conversations with metadata saved to blob storage");
            Console.WriteLine();
        }

        private static async Task RunLanguageIdentification()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Begining language identification ({0})", DateTime.Now.ToString());

            foreach (var conversation in conversations)
            {
                var client = new LanguageClient(textAnalyticsApiKey);

                foreach (var message in conversation.Messages)
                {
                    try
                    {
                        var document = new Document()
                        {
                            Id = message.Id.ToString(),
                            Text = message.Text
                        };

                        var request = new LanguageRequest();
                        request.Documents.Add(document);

                        var response = await client.GetLanguagesAsync(request);

                        message.Metadata.LanguageName = response.Documents[0].DetectedLanguages[0].Iso639Name;
                        message.Metadata.LanguageConfidenceScore = response.Documents[0].DetectedLanguages[0].Score;

                        Console.Write("Conversation {0} | Message {1} | Language {2}", conversation.Id, message.Id, message.Metadata.LanguageName);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine();
                        Console.WriteLine("Exception encountered ({0})", DateTime.Now.ToString());
                        Console.WriteLine("Conversation {0} | Message {1}", conversation.Id, message.Id);
                        Console.WriteLine(ex.Message);
                    }
                }
                Console.WriteLine("Language identification complete ({0})", DateTime.Now.ToString());
                Console.WriteLine();
            }
        }

        private static async Task RunContentModeration()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Begining content moderation ({0})", DateTime.Now.ToString());

            var client = new ModeratorClient(contentModeratorApiKey);

            foreach (var conversation in conversations)
            {
                foreach (var message in conversation.Messages)
                {
                    try
                    {
                        var screenedText = await client.ScreenTextAsync(message.Text, 
                            Constants.MediaType.Plain, message.Metadata.LanguageName, true, true, true, "");

                        if (screenedText.Terms != null)
                        {
                            message.Metadata.ContainsProfanity = true;
                        }
                        else
                        {
                            message.Metadata.ContainsProfanity = false;
                        }

                        Console.Write("Conversation {0} | Message {1} | Contains Profanity {2}", conversation.Id, message.Id, message.Metadata.ContainsProfanity);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine();
                        Console.WriteLine("Exception encountered ({0})", DateTime.Now.ToString());
                        Console.WriteLine("Conversation {0} | Message {1}", conversation.Id, message.Id);
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            Console.WriteLine("Content moderation complete ({0})", DateTime.Now.ToString());
            Console.WriteLine();
        }

        private static async Task RunSentimentAnalysis()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Begining sentiment analysis ({0})", DateTime.Now.ToString());

            var client = new SentimentClient(textAnalyticsApiKey);

            foreach (var conversation in conversations)
            {
                foreach (var message in conversation.Messages)
                {
                    try
                    {
                        var document = new SentimentDocument()
                        {
                            Id = message.Id.ToString(),
                            Text = message.Text,
                            Language = message.Metadata.LanguageName
                        };

                        var request = new SentimentRequest();
                        request.Documents.Add(document);

                        var response = await client.GetSentimentAsync(request);

                        message.Metadata.SentimentScore = response.Documents[0].Score;

                        Console.Write("Conversation {0} | Message {1} | Sentiment Score {2}", conversation.Id, message.Id, message.Metadata.SentimentScore);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine();
                        Console.WriteLine("Exception encountered ({0})", DateTime.Now.ToString());
                        Console.WriteLine("Conversation {0} | Message {1}", conversation.Id, message.Id);
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            Console.WriteLine("Sentiment analysis complete ({0})", DateTime.Now.ToString());
            Console.WriteLine();
        }

        private static async Task RunKeyPhraseDetection()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Begining key phrase detection ({0})", DateTime.Now.ToString());

            var client = new KeyPhraseClient(textAnalyticsApiKey);

            foreach (var conversation in conversations)
            {
                foreach (var message in conversation.Messages)
                {
                    try
                    {
                        var document = new KeyPhraseDocument()
                        {
                            Id = message.Id.ToString(),
                            Text = message.Text,
                            Language = message.Metadata.LanguageName
                        };

                        var request = new KeyPhraseRequest();
                        request.Documents.Add(document);

                        var response = await client.GetKeyPhrasesAsync(request);

                        foreach (var keyPhrase in response.Documents[0].KeyPhrases)
                        {
                            message.Metadata.KeyPhrases.Add(keyPhrase);
                        }

                        Console.Write("Conversation {0} | Message {1} | Key Phrase Count {2}", conversation.Id, message.Id, message.Metadata.KeyPhrases.Count);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine();
                        Console.WriteLine("Exception encountered ({0})", DateTime.Now.ToString());
                        Console.WriteLine("Conversation {0} | Message {1}", conversation.Id, message.Id);
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            Console.WriteLine("Key phrase detection complete ({0})", DateTime.Now.ToString());
            Console.WriteLine();
        }
    }
}