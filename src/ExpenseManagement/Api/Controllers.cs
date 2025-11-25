using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Api;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses with optional filtering
    /// </summary>
    /// <param name="filter">Optional search filter</param>
    /// <param name="status">Optional status filter (Draft, Submitted, Approved, Rejected)</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<Expense>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<Expense>>>> GetAll([FromQuery] string? filter = null, [FromQuery] string? status = null)
    {
        try
        {
            var expenses = await _expenseService.GetAllExpensesAsync(filter, status);
            return Ok(new ApiResponse<List<Expense>> { Success = true, Data = expenses });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses");
            return Ok(new ApiResponse<List<Expense>>
            {
                Success = false,
                Error = ExpenseService.LastError,
                ErrorSource = ExpenseService.LastErrorSource,
                Data = new List<Expense>()
            });
        }
    }

    /// <summary>
    /// Get a specific expense by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<Expense>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<Expense>>> GetById(int id)
    {
        var expense = await _expenseService.GetExpenseByIdAsync(id);
        if (expense == null)
        {
            return NotFound(new ApiResponse<Expense> { Success = false, Error = "Expense not found" });
        }
        return Ok(new ApiResponse<Expense> { Success = true, Data = expense });
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Expense>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<Expense>>> Create([FromBody] ExpenseCreateRequest request)
    {
        try
        {
            var expense = await _expenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = expense.ExpenseId }, new ApiResponse<Expense> { Success = true, Data = expense });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return Ok(new ApiResponse<Expense>
            {
                Success = false,
                Error = ExpenseService.LastError,
                ErrorSource = ExpenseService.LastErrorSource
            });
        }
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> Submit(int id)
    {
        var result = await _expenseService.SubmitExpenseAsync(id);
        return Ok(new ApiResponse<bool> { Success = result, Data = result });
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> Approve(int id, [FromQuery] int reviewerId)
    {
        var result = await _expenseService.ApproveExpenseAsync(id, reviewerId);
        return Ok(new ApiResponse<bool> { Success = result, Data = result });
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> Reject(int id, [FromQuery] int reviewerId)
    {
        var result = await _expenseService.RejectExpenseAsync(id, reviewerId);
        return Ok(new ApiResponse<bool> { Success = result, Data = result });
    }

    /// <summary>
    /// Get all pending approvals
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(ApiResponse<List<Expense>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<Expense>>>> GetPendingApprovals()
    {
        var expenses = await _expenseService.GetPendingApprovalsAsync();
        return Ok(new ApiResponse<List<Expense>> { Success = true, Data = expenses });
    }

    /// <summary>
    /// Get dashboard statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStats>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardStats>>> GetStats()
    {
        var stats = await _expenseService.GetDashboardStatsAsync();
        return Ok(new ApiResponse<DashboardStats> { Success = true, Data = stats });
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public CategoriesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ExpenseCategory>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ExpenseCategory>>>> GetAll()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return Ok(new ApiResponse<List<ExpenseCategory>> { Success = true, Data = categories });
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public StatusesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ExpenseStatus>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ExpenseStatus>>>> GetAll()
    {
        var statuses = await _expenseService.GetStatusesAsync();
        return Ok(new ApiResponse<List<ExpenseStatus>> { Success = true, Data = statuses });
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public UsersController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<User>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<User>>>> GetAll()
    {
        var users = await _expenseService.GetUsersAsync();
        return Ok(new ApiResponse<List<User>> { Success = true, Data = users });
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Send a chat message to the AI assistant
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            var response = await _chatService.SendMessageAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat endpoint");
            return Ok(new ChatResponse
            {
                Success = false,
                Error = ex.Message,
                GenAIEnabled = _chatService.IsGenAIEnabled
            });
        }
    }

    /// <summary>
    /// Check if GenAI is enabled
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetStatus()
    {
        return Ok(new { GenAIEnabled = _chatService.IsGenAIEnabled });
    }
}
