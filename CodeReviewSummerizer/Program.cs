using AIOMux.Clients;

namespace CodeReviewSummerizer;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║   Code Review Summarizer (CodeLlama) ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Type 'exit' to quit");
        Console.WriteLine();

        // Create Ollama client with codellama model (reuse for all reviews)
        var llm = new OllamaClient(model: "codellama");

        // If filepath provided as argument, review it first
        if (args.Length > 0)
        {
            await ReviewCodeFileAsync(args[0], llm);
            Console.WriteLine();
        }

        // Main loop: keep accepting file paths until 'exit'
        while (true)
        {
            Console.Write("Enter file path to review (or 'exit' to quit): ");
            string? inputPath = Console.ReadLine();

            // Exit if user types 'exit' or empty input
            if (string.IsNullOrWhiteSpace(inputPath) || inputPath.Trim().ToLower() == "exit")
            {
                Console.WriteLine("Exiting Code Review Summarizer. Goodbye!");
                break;
            }

            await ReviewCodeFileAsync(inputPath.Trim(), llm);
            Console.WriteLine();
        }
    }

    static async Task ReviewCodeFileAsync(string filePath, OllamaClient llm)
    {
        // Validate file exists
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found: {filePath}");
            return;
        }

        Console.WriteLine($"Reviewing: {Path.GetFileName(filePath)}");
        Console.WriteLine($"Full path: {filePath}");
        Console.WriteLine();

        // Read the code file
        string code;
        try
        {
            code = await File.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            Console.WriteLine("Warning: File is empty.");
            return;
        }

        Console.WriteLine($"File size: {code.Length} characters");
        Console.WriteLine("Analyzing code with CodeLlama...");
        Console.WriteLine();

        // Build review prompt
        string reviewPrompt = BuildReviewPrompt(filePath, code);

        // Get code review from AI
        Console.WriteLine("═══ CODE REVIEW SUMMARY ═══");
        Console.WriteLine();

        try
        {
            string review = await llm.GenerateAsync(reviewPrompt);

            // Print review with slight delay for readability
            foreach (char c in review)
            {
                Console.Write(c);
                await Task.Delay(5);
            }
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during review: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Make sure Ollama is running with codellama model installed.");
            Console.WriteLine("Run: ollama pull codellama");
        }
    }

    static string BuildReviewPrompt(string filePath, string code)
    {
        string fileExtension = Path.GetExtension(filePath).ToLower();
        string fileName = Path.GetFileName(filePath);

        return $@"You are an expert code reviewer. Review the following {fileExtension} code file and provide a concise summary.

        File: {fileName}

        Focus on:
        1. Code quality and best practices
        2. Potential bugs or issues
        3. Security concerns
        4. Performance considerations
        5. Readability and maintainability
        6. Suggestions for improvement

        CODE:
        ```
        {code}
        ```

        Provide a structured review summary:";
    }
}
