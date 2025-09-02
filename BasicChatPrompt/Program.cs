using AIOMux.Clients;

namespace BasicChatPrompt
{
    internal class Program
    {
        // Entry point of the application
        static async Task Main(string[] args)
        {
            // Create an instance of the Ollama LLM client with the specified model
            var llm = new OllamaClient(model: "llama3");

            // Main chat loop: keep accepting user input until 'exit' is typed
            while (true)
            {
                Console.Write("You: ");

                // Prompt user for input
                string? input = Console.ReadLine();

                // Exit the loop if input is empty or 'exit'
                if (string.IsNullOrWhiteSpace(input) || input.Trim().ToLower() == "exit")
                    break;

                // Send the user input to the LLM and get the response
                string response = await llm.GenerateAsync(input);

                Console.Write("AI: ");

                // Print the AI response character by character for a chat-like effect
                foreach (char c in response)
                {
                    Console.Write(c);
                    await Task.Delay(12); // Small delay for realism (adjust as needed)
                }
                Console.WriteLine();
            }
        }
    }
}