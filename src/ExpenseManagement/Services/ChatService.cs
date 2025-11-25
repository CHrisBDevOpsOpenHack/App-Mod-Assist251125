using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using ExpenseManagement.Models;
using System.Text.Json;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request);
    bool IsGenAIEnabled { get; }
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ChatService> _logger;
    private readonly string? _endpoint;
    private readonly string? _deploymentName;
    private readonly string? _managedIdentityClientId;

    public bool IsGenAIEnabled => !string.IsNullOrEmpty(_endpoint);

    public ChatService(IConfiguration configuration, IExpenseService expenseService, ILogger<ChatService> logger)
    {
        _configuration = configuration;
        _expenseService = expenseService;
        _logger = logger;
        _endpoint = configuration["OpenAI:Endpoint"];
        _deploymentName = configuration["OpenAI:DeploymentName"] ?? "gpt-4o";
        _managedIdentityClientId = configuration["ManagedIdentityClientId"];
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
    {
        if (!IsGenAIEnabled)
        {
            return new ChatResponse
            {
                Success = true,
                Message = "GenAI services are not deployed. This is a demo response.\n\nTo enable AI-powered chat:\n1. Run deploy-with-chat.sh instead of deploy.sh\n2. This will deploy Azure OpenAI and AI Search resources\n3. The chat will then be able to help you manage expenses using natural language\n\nFor now, you can use the navigation menu to access Expenses, New Expense, and Approvals pages.",
                GenAIEnabled = false
            };
        }

        try
        {
            TokenCredential credential;
            if (!string.IsNullOrEmpty(_managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", _managedIdentityClientId);
                credential = new ManagedIdentityCredential(_managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var client = new OpenAIClient(new Uri(_endpoint!), credential);

            var chatMessages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(GetSystemPrompt())
            };

            // Add history if provided
            if (request.History != null)
            {
                foreach (var msg in request.History)
                {
                    if (msg.Role == "user")
                        chatMessages.Add(new ChatRequestUserMessage(msg.Content));
                    else if (msg.Role == "assistant")
                        chatMessages.Add(new ChatRequestAssistantMessage(msg.Content));
                }
            }

            chatMessages.Add(new ChatRequestUserMessage(request.Message));

            var options = new ChatCompletionsOptions(_deploymentName, chatMessages)
            {
                Temperature = 0.7f,
                MaxTokens = 1000,
                Functions =
                {
                    new FunctionDefinition("get_all_expenses")
                    {
                        Description = "Retrieves all expenses from the database, optionally filtered by status or search term",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                filter = new { type = "string", description = "Optional search term to filter expenses" },
                                status = new { type = "string", description = "Optional status filter (Draft, Submitted, Approved, Rejected)" }
                            }
                        })
                    },
                    new FunctionDefinition("get_pending_approvals")
                    {
                        Description = "Gets all expenses that are pending approval",
                        Parameters = BinaryData.FromObjectAsJson(new { type = "object", properties = new { } })
                    },
                    new FunctionDefinition("get_dashboard_stats")
                    {
                        Description = "Gets dashboard statistics including total expenses, pending approvals, approved amount",
                        Parameters = BinaryData.FromObjectAsJson(new { type = "object", properties = new { } })
                    },
                    new FunctionDefinition("get_categories")
                    {
                        Description = "Gets all expense categories",
                        Parameters = BinaryData.FromObjectAsJson(new { type = "object", properties = new { } })
                    },
                    new FunctionDefinition("create_expense")
                    {
                        Description = "Creates a new expense",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                userId = new { type = "integer", description = "User ID creating the expense" },
                                categoryId = new { type = "integer", description = "Category ID for the expense" },
                                amount = new { type = "number", description = "Amount in GBP" },
                                expenseDate = new { type = "string", description = "Date of the expense (YYYY-MM-DD)" },
                                description = new { type = "string", description = "Description of the expense" }
                            },
                            required = new[] { "userId", "categoryId", "amount", "expenseDate" }
                        })
                    },
                    new FunctionDefinition("approve_expense")
                    {
                        Description = "Approves a submitted expense",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                expenseId = new { type = "integer", description = "ID of the expense to approve" },
                                reviewerId = new { type = "integer", description = "ID of the manager approving" }
                            },
                            required = new[] { "expenseId", "reviewerId" }
                        })
                    }
                }
            };

            var response = await client.GetChatCompletionsAsync(options);
            var choice = response.Value.Choices[0];

            // Handle function calls
            while (choice.FinishReason == CompletionsFinishReason.FunctionCall)
            {
                var functionCall = choice.Message.FunctionCall;
                var functionResult = await ExecuteFunctionAsync(functionCall.Name, functionCall.Arguments);

                chatMessages.Add(new ChatRequestAssistantMessage(choice.Message));
                chatMessages.Add(new ChatRequestFunctionMessage(functionCall.Name, functionResult));

                options = new ChatCompletionsOptions(_deploymentName, chatMessages)
                {
                    Temperature = 0.7f,
                    MaxTokens = 1000
                };

                response = await client.GetChatCompletionsAsync(options);
                choice = response.Value.Choices[0];
            }

            return new ChatResponse
            {
                Success = true,
                Message = choice.Message.Content,
                GenAIEnabled = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return new ChatResponse
            {
                Success = false,
                Error = ex.Message,
                GenAIEnabled = true
            };
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonDocument.Parse(arguments);

            switch (functionName)
            {
                case "get_all_expenses":
                    string? filter = null;
                    string? status = null;
                    if (args.RootElement.TryGetProperty("filter", out var filterProp))
                        filter = filterProp.GetString();
                    if (args.RootElement.TryGetProperty("status", out var statusProp))
                        status = statusProp.GetString();
                    var expenses = await _expenseService.GetAllExpensesAsync(filter, status);
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = $"£{e.Amount:F2}",
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.StatusName,
                        e.Description
                    }));

                case "get_pending_approvals":
                    var pending = await _expenseService.GetPendingApprovalsAsync();
                    return JsonSerializer.Serialize(pending.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = $"£{e.Amount:F2}",
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.Description
                    }));

                case "get_dashboard_stats":
                    var stats = await _expenseService.GetDashboardStatsAsync();
                    return JsonSerializer.Serialize(new
                    {
                        stats.TotalExpenses,
                        stats.PendingApprovals,
                        ApprovedAmount = $"£{stats.ApprovedAmount:F2}",
                        stats.ApprovedCount
                    });

                case "get_categories":
                    var categories = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(categories);

                case "create_expense":
                    var createRequest = new ExpenseCreateRequest
                    {
                        UserId = args.RootElement.GetProperty("userId").GetInt32(),
                        CategoryId = args.RootElement.GetProperty("categoryId").GetInt32(),
                        Amount = args.RootElement.GetProperty("amount").GetDecimal(),
                        ExpenseDate = DateTime.Parse(args.RootElement.GetProperty("expenseDate").GetString()!),
                        Description = args.RootElement.TryGetProperty("description", out var descProp) ? descProp.GetString() : null
                    };
                    var created = await _expenseService.CreateExpenseAsync(createRequest);
                    return JsonSerializer.Serialize(new { Success = true, created.ExpenseId });

                case "approve_expense":
                    var expenseId = args.RootElement.GetProperty("expenseId").GetInt32();
                    var reviewerId = args.RootElement.GetProperty("reviewerId").GetInt32();
                    var approved = await _expenseService.ApproveExpenseAsync(expenseId, reviewerId);
                    return JsonSerializer.Serialize(new { Success = approved });

                default:
                    return JsonSerializer.Serialize(new { Error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { Error = ex.Message });
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are an AI assistant for the Expense Management System. You help users manage their expenses, view pending approvals, and get insights about their spending.

Available capabilities:
- List all expenses or filter by status/search term
- View pending approval requests
- Get dashboard statistics (total expenses, pending approvals, approved amounts)
- View expense categories
- Create new expenses
- Approve expenses (for managers)

When listing items, format them nicely with:
- Numbered lists for multiple items
- Bold for important values like amounts
- Clear date formatting

Always be helpful and provide clear, concise responses. If a user wants to create an expense, ask for the required details: category, amount, date, and description.

For approving expenses, remind users that only managers can approve expenses.";
    }
}
