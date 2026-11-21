namespace Lunate.Contracts;

/// <summary>
/// The ordered probability assigned to one defined answer, as a decision event publishes it. The
/// decision model itself lives in <c>Lunate.Decisions.Model</c>; this one record stays in the contract
/// because <see cref="DecisionEvaluated.Distribution"/> exposes it to every event consumer.
/// </summary>
public sealed record DecisionProbability(string Answer, double Probability);
