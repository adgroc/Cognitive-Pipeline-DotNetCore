using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pipeline
{
    public class Conversation
    {
        public Conversation()
        {
            Messages = new List<Message>();
        }

        [JsonProperty("conversationId")]
        public Guid Id { get; set; }

        [JsonProperty("messages")]
        public List<Message> Messages { get; set; }
    }

    public class Message
    {
        public Message()
        {
            Metadata = new MessageMetadata();
        }

        [JsonProperty("messageId")]
        public Guid Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("metadata")]
        public MessageMetadata Metadata { get; set; }
    }

    public class MessageMetadata
    {
        public MessageMetadata()
        {
            KeyPhrases = new List<string>();
        }

        [JsonProperty("containsProfanity")]
        public bool ContainsProfanity { get; set; }

        [JsonProperty("languageName")]
        public string LanguageName { get; set; }

        [JsonProperty("languageConfidenceScore")]
        public float LanguageConfidenceScore { get; set; }

        [JsonProperty("sentimentScore")]
        public float SentimentScore { get; set; }

        [JsonProperty("keyPhrases")]
        public List<string> KeyPhrases { get; set; }
    }
}
