using OpenAI_API;
using OpenAI_API.Chat;

namespace OCore.Copilot.Core
{
    public static class Service
    {
        private static OpenAIAPI? api;

        public static void CheckInit()
        {
            if (api == null)
            {
                throw new Exception("OpenAI API must be initialized with a call to SetupApi");
            }
        }

        public static void SetupApi(string apiKey, string? organization = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("Missing apiKey");
            }

            if (string.IsNullOrWhiteSpace(organization))
            {
                api = new OpenAIAPI(new APIAuthentication(apiKey));
            }
            else
            {
                api = new OpenAIAPI(new APIAuthentication(apiKey, organization));
            }
        }

        public static Conversation CreateConversation(string? model = null)
        {
            CheckInit();
            //return api!.Chat.CreateConversation(new ChatRequest
            //{
            //    Model = "gpt-4"
            //});
            if (model == null)
            {
                return api!.Chat.CreateConversation();
            }
            else throw new Exception("Currently, only the default model is supported");
        }

        public static void AddSystemMessage(Conversation conversation, string message)
        {
            conversation.AppendSystemMessage(message);
        }

        public static void AddExample(Conversation conversation, string input, string output)
        {
            conversation.AppendUserInput(input);
            conversation.AppendExampleChatbotOutput(output);
        }

        public static void AddInput(Conversation conversation, string input)
        {
            conversation.AppendUserInput(input);
        }

        public static async Task<string> GetResponse(Conversation conversation)
        {
            return await conversation.GetResponseFromChatbotAsync();
        }

        public static IAsyncEnumerable<string> GetStream(Conversation conversation)
        {
            return conversation.StreamResponseEnumerableFromChatbotAsync();
        }
    }
}