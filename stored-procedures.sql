-- Stored Procedures for Expense Management System
-- Execute this file after the database schema is created

SET NOCOUNT ON;
GO

-- Get all expenses with optional filtering
CREATE OR ALTER PROCEDURE usp_GetAllExpenses
    @Filter NVARCHAR(500) = NULL,
    @Status NVARCHAR(50) = NULL
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE (@Filter IS NULL OR 
           e.Description LIKE '%' + @Filter + '%' OR
           u.UserName LIKE '%' + @Filter + '%' OR
           c.CategoryName LIKE '%' + @Filter + '%')
      AND (@Status IS NULL OR s.StatusName = @Status)
    ORDER BY e.CreatedAt DESC;
END;
GO

-- Get expense by ID
CREATE OR ALTER PROCEDURE usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE e.ExpenseId = @ExpenseId;
END;
GO

-- Create new expense
CREATE OR ALTER PROCEDURE usp_CreateExpense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL
AS
BEGIN
    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';
    
    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, CreatedAt)
    VALUES (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, 'GBP', @ExpenseDate, @Description, SYSUTCDATETIME());
    
    DECLARE @NewExpenseId INT = SCOPE_IDENTITY();
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE e.ExpenseId = @NewExpenseId;
END;
GO

-- Submit expense for approval
CREATE OR ALTER PROCEDURE usp_SubmitExpense
    @ExpenseId INT
AS
BEGIN
    DECLARE @SubmittedStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';
    
    UPDATE dbo.Expenses
    SET StatusId = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- Approve expense
CREATE OR ALTER PROCEDURE usp_ApproveExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    DECLARE @ApprovedStatusId INT;
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';
    
    UPDATE dbo.Expenses
    SET StatusId = @ApprovedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- Reject expense
CREATE OR ALTER PROCEDURE usp_RejectExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    DECLARE @RejectedStatusId INT;
    SELECT @RejectedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';
    
    UPDATE dbo.Expenses
    SET StatusId = @RejectedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- Get pending approvals
CREATE OR ALTER PROCEDURE usp_GetPendingApprovals
AS
BEGIN
    DECLARE @SubmittedStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE e.StatusId = @SubmittedStatusId
    ORDER BY e.SubmittedAt ASC;
END;
GO

-- Get categories
CREATE OR ALTER PROCEDURE usp_GetCategories
AS
BEGIN
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END;
GO

-- Get statuses
CREATE OR ALTER PROCEDURE usp_GetStatuses
AS
BEGIN
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END;
GO

-- Get users
CREATE OR ALTER PROCEDURE usp_GetUsers
AS
BEGIN
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.IsActive
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END;
GO

-- Get dashboard stats
CREATE OR ALTER PROCEDURE usp_GetDashboardStats
AS
BEGIN
    DECLARE @ApprovedStatusId INT;
    DECLARE @SubmittedStatusId INT;
    
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';
    
    SELECT
        (SELECT COUNT(*) FROM dbo.Expenses) AS TotalExpenses,
        (SELECT COUNT(*) FROM dbo.Expenses WHERE StatusId = @SubmittedStatusId) AS PendingApprovals,
        ISNULL((SELECT SUM(AmountMinor) / 100.0 FROM dbo.Expenses WHERE StatusId = @ApprovedStatusId), 0) AS ApprovedAmount,
        (SELECT COUNT(*) FROM dbo.Expenses WHERE StatusId = @ApprovedStatusId) AS ApprovedCount;
END;
GO

PRINT 'All stored procedures created successfully!';
GO
