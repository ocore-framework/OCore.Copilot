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

        public static Conversation CreateConversation()
        {
            CheckInit();
            return api!.Chat.CreateConversation();
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