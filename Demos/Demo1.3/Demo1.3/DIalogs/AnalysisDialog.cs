using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace demobot.DIalogs
{
    using System.Diagnostics;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using demobot.Model;
    using demobot.Utils;

    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Dialogs.Internals;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Connector;

    using Newtonsoft.Json;

    [LuisModel("3cb85d0d-1594-4cd0-a89a-e8bb846605d0", "0651edc9023246549bbf4e0d6149afba")]
    [Serializable]
    public class AnalysisDialog : LuisDialog<object>
    {
        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"I've got nothing or do not understand what you are saying.");
            context.Wait(this.MessageReceived);
        }

        [LuisIntent("help")]
        public async Task Help(IDialogContext context, LuisResult result)
        {
            await
                context.PostAsync(
                    $"This bot can do sentence analysis for you.  If you are curious about how a sentence breaks down, try asking 'analyse the grammar for this sentence' then when prompted just type a sentence.  If you want to checkout the sentiment of a statement or website, just ask. 'what is the sentiment for this sentence' or just ask 'what is the sentiment for this website' then when prompted give it the web page address or a sentence.");
            context.Wait(this.MessageReceived);
        }

        [LuisIntent("linguisticanalysis")]
        public async Task LinguisticAnalysis(IDialogContext context, LuisResult result)
        {
            StringBuilder message = new StringBuilder("");
            message.Append($"I received your grammar request, please now tell me the sentence you want analyzed.");
            PromptDialog.Text(context, this.OnReceiveSentence, message.ToString(), null, 3);
        }

        [LuisIntent("textanalytics")]
        public async Task TextAnalytics(IDialogContext context, LuisResult result)
        {
            string analyticsType = "";
            if (result.Entities != null && result.Entities.Count > 0)
            {
                analyticsType = result.Entities[0].Entity;
            }

            if (!string.IsNullOrEmpty(analyticsType))
            {
                if (analyticsType == "sentence" || analyticsType == "statement")
                {
                    StringBuilder message = new StringBuilder("");
                    message.Append(
                        $"You have requested text analytics for a {analyticsType}. Please type in the {analyticsType} you want me to analyze now.");
                    PromptDialog.Text(context, this.OnReceiveSentenceForTextAnalytics, message.ToString(), null, 3);
                }
                else if (analyticsType == "web site" || analyticsType == "website" || analyticsType == "web page"
                         || analyticsType == "webpage")
                {
                    StringBuilder message = new StringBuilder("");
                    message.Append(
                        $"You have requested text analytics for a {analyticsType}. Please type in the address for the {analyticsType} you want me to analyze now.");
                    PromptDialog.Text(context, this.OnReceiveUrlForTextAnalytics, message.ToString(), null, 3);
                }
                else
                {
                    StringBuilder message = new StringBuilder("");
                    message.Append(
                        $"You didn't specify what you want analyzed.  How about we try a sentence you type in? Please type in a sentence you want me to analyze now.");
                    PromptDialog.Text(context, this.OnReceiveSentenceForTextAnalytics, message.ToString(), null, 3);
                }
            }
            else
            {
                StringBuilder message = new StringBuilder("");
                message.Append(
                    $"You didn't specify what you want analyzed.  How about we try a sentence you type in? Please type in a sentence you want me to analyze now.");
                PromptDialog.Text(context, this.OnReceiveSentenceForTextAnalytics, message.ToString(), null, 3);
            }
        }

        private async Task OnReceiveSentence(IDialogContext context, IAwaitable<object> result)
        {
            var received = await result;
            var sentence = received.ToString();
            string checkSentence = await SpellChecker.CorrectSpelling(received.ToString());
            if (checkSentence != received.ToString())
            {
                sentence = checkSentence;
                await
                    context.PostAsync(
                        $"I found at least one spelling mistake that I have fixed for you. I'm going to analyze the corrected sentence: '**{sentence}**'.");
            }
            await this.AnalyzeSpeechParts(context, sentence);
        }

        private async Task AnalyzeSpeechParts(IDialogContext context, string sentence)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(String.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "70e9d2e1b0b84ec0a863458f2be1af62");

            var uri = "https://westus.api.cognitive.microsoft.com/linguistics/v1.0/analyze?" + queryString;

            HttpResponseMessage response;
            var body = new LinguisticRequest { Language = "en", Text = sentence, AnalyzerIds = new List<string>() };          
            body.AnalyzerIds.Add("22a6b758-420f-4745-8a3c-46835a67c0d2");
            var jsonBody = JsonConvert.SerializeObject(body);
            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes(jsonBody);
            List<LinquisticResponse> linguisticResponse;
            try
            {
                using (var content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    response = await client.PostAsync(uri, content);
                    var json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine(json);
                    linguisticResponse = JsonConvert.DeserializeObject<List<LinquisticResponse>>(json);
                }
                if (linguisticResponse != null)
                {
                    GrammarStatistics.ParseParts(linguisticResponse[0]);
                    string result = GrammarStatistics.AnalysisResult;
                    var message = context.MakeMessage();
                    message.Text = result.Replace("\r\n", "\n\n");
                    await context.PostAsync(message);
                }
            }
            catch (Exception ex)
            {
                // Place exception handling here.
            }
            context.Wait(this.MessageReceived);
        }

        private async Task OnReceiveSentenceForTextAnalytics(IDialogContext context, IAwaitable<object> result)
        {
            var received = await result;
            var sentence = received.ToString();
            await this.AnalyzeText(context, sentence);
        }

        private async Task OnReceiveUrlForTextAnalytics(IDialogContext context, IAwaitable<object> result)
        {
            var received = await result;
            var uriString = received.ToString();
            HttpClient client = new HttpClient();
            string downloadString = await client.GetStringAsync(new Uri(uriString, UriKind.Absolute));
            await this.AnalyzeText(context, downloadString.Substring(0, 1000));
        }

        private async Task AnalyzeText(IDialogContext context, string sentence)
        {
            // 57af6a7afd87438497231387d94767ba
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(String.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "740cbdd34284421cb42e8fb85d4da338");

            var uri = "https://westus.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment?" + queryString;

            HttpResponseMessage response;
            var body = new TextAnalyticsRequest
            {
                Documents = new List<RequestDocument>
                {
                    new RequestDocument
                        {
                            Language = "en",
                            Text = sentence,
                            Id = "primary"
                        }
                } 
            };
            var jsonBody = JsonConvert.SerializeObject(body);

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes(jsonBody);
            TextAnalyticsResponse textAnalyticsResponse;
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(json);
                textAnalyticsResponse = JsonConvert.DeserializeObject<TextAnalyticsResponse>(json);
            }
            if (textAnalyticsResponse != null)
            {
                var message = context.MakeMessage();
                var responseCard = new HeroCard();
                if (textAnalyticsResponse.documents[0].score > .80)
                    responseCard.Images = new List<CardImage> { new CardImage("http://localhost:3979/Assets/Star5.png") };
                else if (textAnalyticsResponse.documents[0].score > .60)
                    responseCard.Images = new List<CardImage> { new CardImage("http://localhost:3979/Assets/Star4.png") };
                else if (textAnalyticsResponse.documents[0].score > .40)
                    responseCard.Images = new List<CardImage> { new CardImage("http://localhost:3979/Assets/Star3.png") };
                else if (textAnalyticsResponse.documents[0].score > .20)
                    responseCard.Images = new List<CardImage> { new CardImage("http://localhost:3979/Assets/Star2.png") };
                else
                    responseCard.Images = new List<CardImage> { new CardImage("http://localhost:3979/Assets/Star1.png") };

                responseCard.Title = "Text Analytics";
                responseCard.Text = $"Your entered sentence is {Math.Truncate(textAnalyticsResponse.documents[0].score * 100)}% positive.";
                message.Attachments.Add(responseCard.ToAttachment());
                await context.PostAsync(message);
                context.Wait(this.MessageReceived);
            }
        }

    }

}