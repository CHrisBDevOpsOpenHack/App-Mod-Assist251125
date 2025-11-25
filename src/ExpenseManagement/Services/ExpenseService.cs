using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;
using System.Data;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<List<Expense>> GetAllExpensesAsync(string? filter = null, string? status = null);
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<Expense> CreateExpenseAsync(ExpenseCreateRequest request);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<List<Expense>> GetPendingApprovalsAsync();
    Task<List<ExpenseCategory>> GetCategoriesAsync();
    Task<List<ExpenseStatus>> GetStatusesAsync();
    Task<List<User>> GetUsersAsync();
    Task<DashboardStats> GetDashboardStatsAsync();
}

public class ExpenseService : IExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;
    private static bool _useDummyData = false;
    private static string _lastError = string.Empty;
    private static string _lastErrorSource = string.Empty;

    public static bool UseDummyData => _useDummyData;
    public static string LastError => _lastError;
    public static string LastErrorSource => _lastErrorSource;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string not configured");
        _logger = logger;
    }

    private async Task<SqlConnection> GetConnectionAsync()
    {
        try
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            _useDummyData = false;
            return connection;
        }
        catch (Exception ex)
        {
            _useDummyData = true;
            _lastError = ex.Message;
            _lastErrorSource = $"{nameof(ExpenseService)}.{nameof(GetConnectionAsync)}";
            _logger.LogError(ex, "Failed to connect to database. Using dummy data. Error: {Error}", ex.Message);
            
            if (ex.Message.Contains("Managed Identity") || ex.Message.Contains("authentication"))
            {
                _lastError = $"Managed Identity Error: {ex.Message}. Fix: Ensure the managed identity has been granted access to the database using 'CREATE USER [identity-name] FROM EXTERNAL PROVIDER' and appropriate role assignments (db_datareader, db_datawriter).";
            }
            
            throw;
        }
    }

    public async Task<List<Expense>> GetAllExpensesAsync(string? filter = null, string? status = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetAllExpenses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@Filter", (object?)filter ?? DBNull.Value);
            command.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses");
            SetError(ex, nameof(GetAllExpensesAsync));
            return GetDummyExpenses();
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetExpenseById", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpense(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense by id {ExpenseId}", expenseId);
            SetError(ex, nameof(GetExpenseByIdAsync));
            return GetDummyExpenses().FirstOrDefault();
        }
    }

    public async Task<Expense> CreateExpenseAsync(ExpenseCreateRequest request)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_CreateExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpense(reader);
            }
            throw new InvalidOperationException("Failed to create expense");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            SetError(ex, nameof(CreateExpenseAsync));
            return new Expense
            {
                ExpenseId = new Random().Next(1000, 9999),
                UserId = request.UserId,
                CategoryId = request.CategoryId,
                AmountMinor = (int)(request.Amount * 100),
                ExpenseDate = request.ExpenseDate,
                Description = request.Description,
                StatusName = "Draft",
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_SubmitExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", expenseId);
            SetError(ex, nameof(SubmitExpenseAsync));
            return true; // Return true for dummy data mode
        }
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_ApproveExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", expenseId);
            SetError(ex, nameof(ApproveExpenseAsync));
            return true;
        }
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_RejectExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {ExpenseId}", expenseId);
            SetError(ex, nameof(RejectExpenseAsync));
            return true;
        }
    }

    public async Task<List<Expense>> GetPendingApprovalsAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetPendingApprovals", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending approvals");
            SetError(ex, nameof(GetPendingApprovalsAsync));
            return GetDummyExpenses().Where(e => e.StatusName == "Submitted").ToList();
        }
    }

    public async Task<List<ExpenseCategory>> GetCategoriesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetCategories", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var categories = new List<ExpenseCategory>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            SetError(ex, nameof(GetCategoriesAsync));
            return GetDummyCategories();
        }
    }

    public async Task<List<ExpenseStatus>> GetStatusesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetStatuses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var statuses = new List<ExpenseStatus>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }
            return statuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statuses");
            SetError(ex, nameof(GetStatusesAsync));
            return GetDummyStatuses();
        }
    }

    public async Task<List<User>> GetUsersAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetUsers", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var users = new List<User>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? null : reader.GetString(reader.GetOrdinal("RoleName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            SetError(ex, nameof(GetUsersAsync));
            return GetDummyUsers();
        }
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetDashboardStats", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new DashboardStats
                {
                    TotalExpenses = reader.GetInt32(reader.GetOrdinal("TotalExpenses")),
                    PendingApprovals = reader.GetInt32(reader.GetOrdinal("PendingApprovals")),
                    ApprovedAmount = reader.GetDecimal(reader.GetOrdinal("ApprovedAmount")),
                    ApprovedCount = reader.GetInt32(reader.GetOrdinal("ApprovedCount"))
                };
            }
            return new DashboardStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            SetError(ex, nameof(GetDashboardStatsAsync));
            return GetDummyStats();
        }
    }

    private void SetError(Exception ex, string methodName)
    {
        _useDummyData = true;
        _lastError = ex.Message;
        _lastErrorSource = $"{nameof(ExpenseService)}.{methodName}";
        
        if (ex.Message.Contains("Managed Identity") || ex.Message.Contains("authentication") || ex.Message.Contains("Login failed"))
        {
            _lastError = $"Managed Identity Error: {ex.Message}. Fix: 1) Ensure the managed identity is created in Azure. 2) Run the database role assignment script (run-sql-dbrole.py) to grant database access using 'CREATE USER [identity-name] FROM EXTERNAL PROVIDER'. 3) Assign roles: db_datareader, db_datawriter, EXECUTE permission.";
        }
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.IsDBNull(reader.GetOrdinal("StatusName")) ? null : reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    // Dummy data methods for fallback
    private static List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new() { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-5), Description = "Taxi from airport to client site", CreatedAt = DateTime.Now.AddDays(-5) },
            new() { ExpenseId = 2, UserId = 1, UserName = "Alice Example", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 1425, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-10), Description = "Client lunch meeting", CreatedAt = DateTime.Now.AddDays(-10) },
            new() { ExpenseId = 3, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", AmountMinor = 799, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-1), Description = "Office stationery", CreatedAt = DateTime.Now.AddDays(-1) },
            new() { ExpenseId = 4, UserId = 1, UserName = "Alice Example", CategoryId = 4, CategoryName = "Accommodation", StatusId = 3, StatusName = "Approved", AmountMinor = 12300, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-30), Description = "Hotel during client visit", CreatedAt = DateTime.Now.AddDays(-30) }
        };
    }

    private static List<ExpenseCategory> GetDummyCategories()
    {
        return new List<ExpenseCategory>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private static List<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };
    }

    private static List<User> GetDummyUsers()
    {
        return new List<User>
        {
            new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true },
            new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true }
        };
    }

    private static DashboardStats GetDummyStats()
    {
        return new DashboardStats
        {
            TotalExpenses = 10,
            PendingApprovals = 1,
            ApprovedAmount = 519.24m,
            ApprovedCount = 6
        };
    }
}
