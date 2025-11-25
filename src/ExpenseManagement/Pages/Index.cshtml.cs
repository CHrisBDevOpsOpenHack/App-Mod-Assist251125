using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public DashboardStats Stats { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? ErrorSource { get; set; }

    public IndexModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        Stats = await _expenseService.GetDashboardStatsAsync();
        RecentExpenses = await _expenseService.GetAllExpensesAsync();

        if (ExpenseService.UseDummyData)
        {
            ErrorMessage = ExpenseService.LastError;
            ErrorSource = ExpenseService.LastErrorSource;
        }
    }
}
