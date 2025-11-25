using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<Expense> Expenses { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();
    public string? Filter { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorSource { get; set; }

    public ExpensesModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync(string? filter = null, string? status = null)
    {
        Filter = filter;
        Status = status;
        Expenses = await _expenseService.GetAllExpensesAsync(filter, status);
        Statuses = await _expenseService.GetStatusesAsync();

        if (ExpenseService.UseDummyData)
        {
            ErrorMessage = ExpenseService.LastError;
            ErrorSource = ExpenseService.LastErrorSource;
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        await _expenseService.SubmitExpenseAsync(id);
        return RedirectToPage();
    }
}
