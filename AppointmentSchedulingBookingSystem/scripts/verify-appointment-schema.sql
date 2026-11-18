-- Run against the migrated development database after starting the app once.
-- All test changes are rolled back, including when a test fails.
SET NOCOUNT ON;
SET XACT_ABORT OFF;
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @customer int = (SELECT c.Id FROM Customers c JOIN AspNetUsers u ON u.Id = c.UserId WHERE u.UserName = 'customer');
    DECLARE @consultant int = (SELECT c.Id FROM Consultants c JOIN AspNetUsers u ON u.Id = c.UserId WHERE u.UserName = 'consultant');
    IF @customer IS NULL OR @consultant IS NULL THROW 51000, 'Development profiles must be seeded first.', 1;
    INSERT INTO Appointments (CustomerId, ConsultantId, Date, StartTime, EndTime, Status)
    VALUES (@customer, @consultant, '2099-01-03', '09:00', '09:30', 0);
    DECLARE @appointment int = SCOPE_IDENTITY();
    BEGIN TRY
        INSERT INTO Appointments (CustomerId, ConsultantId, Date, StartTime, EndTime, Status) VALUES (@customer,@consultant,'2099-01-03','09:00','09:30',0);
        THROW 51000, 'Duplicate active start is rejected: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
    END CATCH;
    BEGIN TRY
        INSERT INTO Appointments (CustomerId, ConsultantId, Date, StartTime, EndTime, Status) VALUES (@customer,@consultant,'2099-01-03','10:00','09:30',0);
        THROW 51000, 'Invalid appointment time is rejected: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        UPDATE Appointments SET Status = 99 WHERE Id = @appointment;
        THROW 51000, 'Invalid status is rejected: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        DELETE FROM Customers WHERE Id = @customer;
        THROW 51000, 'Customer history is protected: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        DELETE FROM Consultants WHERE Id = @consultant;
        THROW 51000, 'Consultant history is protected: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        UPDATE Consultants SET SlotDurationMinutes = 0 WHERE Id = @consultant;
        THROW 51000, 'Invalid slot duration is rejected: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        INSERT INTO ConsultantBreaks (ConsultantId,StartTime,EndTime) VALUES (@consultant,'12:00','13:00');
        THROW 51000, 'Break needs a day or date: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        INSERT INTO ConsultantBreaks (ConsultantId,DayOfWeek,Date,StartTime,EndTime) VALUES (@consultant,6,'2099-01-03','12:00','13:00');
        THROW 51000, 'Break cannot have both day and date: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        INSERT INTO ConsultantBreaks (ConsultantId,Date,StartTime,EndTime) VALUES (@consultant,'2099-01-03','13:00','12:00');
        THROW 51000, 'Break time must increase: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        INSERT INTO ConsultantAvailabilities (ConsultantId,DayOfWeek,StartTime,EndTime) VALUES (@consultant,7,'09:00','17:00');
        THROW 51000, 'Availability weekday must be valid: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        UPDATE ConsultantAvailabilities SET EndTime=StartTime WHERE ConsultantId=@consultant;
        THROW 51000, 'Availability time must increase: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    BEGIN TRY
        INSERT INTO Appointments (CustomerId, ConsultantId, Date, StartTime, EndTime, Status) VALUES (-1,@consultant,'2099-01-03','11:00','11:30',0);
        THROW 51000, 'Invalid customer reference is rejected: FAILED', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (547) THROW;
    END CATCH;
    UPDATE Appointments SET Status = 1 WHERE Id = @appointment;
    INSERT INTO Appointments (CustomerId, ConsultantId, Date, StartTime, EndTime, Status)
    VALUES (@customer,@consultant,'2099-01-03','09:00','09:30',0);
    INSERT INTO ConsultantBreaks (ConsultantId,Date,StartTime,EndTime)
    VALUES (@consultant,'2099-01-03','12:00','13:00');
    ROLLBACK TRANSACTION;
    SELECT 'PASS: constraints, cancellation reuse, dated breaks, and history protection' AS Result;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
