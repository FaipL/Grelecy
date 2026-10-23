// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics;

public enum ObservationKind { Problem, Working }

public enum ProblemCategory { Unspecified, WontStart, VisualIssue, Crash, Slow, Other }

// All these fields are statements supplied by the user, separate from automatic collection.
public sealed record UserObservation(
    ObservationKind Kind,
    string SoftwareName,
    string SoftwareVersion,
    string TestedGpu,
    ProblemCategory Problem,
    string Steps,
    string Expected,
    string Observed,
    string Duration,
    string Settings,
    string Limitations);

// CreatedAt describes preparation of this report, not the time of an experiment or a collection.
public sealed record ObservationReport(
    DateTimeOffset CreatedAt,
    UserObservation Observation,
    DiagnosticReport? Configuration);
