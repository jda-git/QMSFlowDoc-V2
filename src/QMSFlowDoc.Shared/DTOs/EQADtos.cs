using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public record EQAProgramDto(
    Guid Id,
    string Name,
    string? Provider,
    string? Frequency,
    EQAStatus Status,
    string? LastResult, // e.g. "SATISFACTORY (2024-01)"
    string? LastResultColor, // Green/Red
    int PendingCount
);

public record CreateEQAProgramRequest(
    string Name,
    string? Provider,
    string? Frequency,
    string? Notes
);

public record UpdateEQAProgramRequest(
    Guid Id,
    string Name,
    string? Provider,
    string? Frequency,
    EQAStatus Status,
    string? Notes
);

public record RegisterEQAResultRequest(
    Guid ProgramId,
    string CycleIdentifier,
    DateTime? ReceiptDate,
    DateTime? ProcessingDate,
    DateTime? SubmissionDate,
    string? Notes
);

public record UpdateEQAResultRequest(
    Guid Id,
    EQAResultStatus Status,
    decimal? Score,
    EQAPerformance Performance,
    string? Notes,
    Guid? ReviewerUserId,
    DateTime? ReviewDate
);

public record EQAResultDto(
    Guid Id,
    Guid ProgramId,
    string CycleIdentifier,
    string Status,
    string Performance,
    string PerformanceColor,
    DateTime? SubmissionDate,
    decimal? Score,
    string? Notes
);

public record EQAProviderDto(
    Guid Id,
    string Name,
    string? Code,
    string? ContactInfo,
    bool IsActive
);

public record EQASchemeDto(
    Guid Id,
    string Name,
    string ProviderName,
    string? ProviderReference,
    string? Periodicity,
    string Status,
    string? Notes,
    int TotalRounds,
    int ValidRounds,
    int UnsatisfactoryRounds,
    string? Matrix = null,
    Guid? ResponsibleUserId = null,
    Guid ProviderId = default
)
{
    // Legacy constructor for LocalDocumentStore compatibility
    public EQASchemeDto(Guid id, Guid providerId, string providerName, string name, string? matrix, string? periodicity, Guid? responsibleUserId, string? notes)
        : this(id, name, providerName, null, periodicity, "UNKNOWN", notes, 0, 0, 0, matrix, responsibleUserId, providerId)
    {
    }
}

public record EQARoundDto(
    Guid Id,
    Guid SchemeId,
    string SchemeName,
    string RoundCode,
    int Year,
    DateTime? DateReceived,
    DateTime? DateAnalysis,
    DateTime? DateDeadline,
    DateTime? DateSubmitted,
    DateTime? DateReportReceived,
    DateTime? DateClosed,
    string Status, // OPEN, CLOSED, REVIEWED
    string? FolderPath,
    string? Notes,
    Guid? ReviewerUserId,
    string? ReviewerName,
    DateTime? ReviewDate
);

public record EQARoundResultDto(
    Guid Id,
    Guid RoundId,
    string? ParameterName,
    string? ResultValue,
    string? Unit,
    string? TargetValue,
    string? Deviation,
    double? ZScore,
    string? Performance,
    decimal? Score,
    string? Notes,
    string? InternalStatus
);
